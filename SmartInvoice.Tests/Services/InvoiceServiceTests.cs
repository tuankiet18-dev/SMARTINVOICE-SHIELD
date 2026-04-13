using FluentAssertions;
using Moq;
using SmartInvoice.API.Entities;
using SmartInvoice.API.Entities.JsonModels;
using SmartInvoice.API.Repositories.Interfaces;
using SmartInvoice.API.Services.Implementations;
using SmartInvoice.API.Services.Interfaces;
using SmartInvoice.API.Services;
using SmartInvoice.Tests.Helpers;
using Microsoft.Extensions.Logging;

namespace SmartInvoice.Tests.Services;

/// <summary>
/// Unit Tests cho InvoiceService — tầng Business Logic cốt lõi.
/// 
/// Chiến lược:
///   - Tất cả dependency được mock bằng Moq (không cần DB thật, AWS, v.v.)
///   - Mỗi test kiểm tra đúng 1 hành vi (single responsibility)
///   - Đặt tên theo pattern: Method_Condition_ExpectedBehavior
/// </summary>
public class InvoiceServiceTests
{
    // ─────────────────────────────────────────
    //  Shared mock objects (re-created mỗi test)
    // ─────────────────────────────────────────
    private readonly Mock<IUnitOfWork>               _mockUow;
    private readonly Mock<IInvoiceRepository>        _mockInvoiceRepo;
    private readonly Mock<IInvoiceAuditLogRepository> _mockAuditLogRepo;
    private readonly Mock<INotificationService>       _mockNotification;
    private readonly Mock<IQuotaService>              _mockQuota;
    private readonly Mock<StorageService>             _mockStorage;
    private readonly InvoiceService                   _sut;

    public InvoiceServiceTests()
    {
        _mockUow          = new Mock<IUnitOfWork>();
        _mockInvoiceRepo  = new Mock<IInvoiceRepository>();
        _mockAuditLogRepo = new Mock<IInvoiceAuditLogRepository>();
        _mockNotification = new Mock<INotificationService>();
        _mockQuota        = new Mock<IQuotaService>();

        // StorageService phụ thuộc vào IAmazonS3 — tạo null-safe mock
        _mockStorage = new Mock<StorageService>(null!, null!);

        // Wiring IUnitOfWork → các repo mock
        _mockUow.Setup(u => u.Invoices).Returns(_mockInvoiceRepo.Object);
        _mockUow.Setup(u => u.InvoiceAuditLogs).Returns(_mockAuditLogRepo.Object);
        _mockUow.Setup(u => u.CompleteAsync()).ReturnsAsync(1);

        // Mặc định: các phương thức async trả về Task.CompletedTask
        _mockAuditLogRepo.Setup(r => r.AddAsync(It.IsAny<InvoiceAuditLog>()))
            .Returns(Task.CompletedTask);
        _mockNotification.Setup(n => n.SendNotificationToCompanyAdminsAsync(
            It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<string>(),
            It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<string?>(),
            It.IsAny<Guid?>(), It.IsAny<string>()))
            .Returns(Task.CompletedTask);
        _mockNotification.Setup(n => n.SendNotificationAsync(
            It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<string>(),
            It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<string?>(),
            It.IsAny<Guid?>(), It.IsAny<string>()))
            .Returns(Task.CompletedTask);

        // Tạo InvoiceService với tất cả dependency đã mock
        // AppDbContext = null! (không dùng cho các test này — những method dùng _context sẽ test riêng)
        _sut = new InvoiceService(
            context:           null!,
            unitOfWork:        _mockUow.Object,
            storageService:    _mockStorage.Object,
            invoiceProcessor:  Mock.Of<IInvoiceProcessorService>(),
            configuration:     null!,
            logger:            Mock.Of<ILogger<InvoiceService>>(),
            sqsPublisher:      Mock.Of<ISqsMessagePublisher>(),
            notificationService: _mockNotification.Object,
            quotaService:      _mockQuota.Object,
            configProvider:    Mock.Of<ISystemConfigProvider>()
        );
    }

    // ═══════════════════════════════════════════════════════════════
    //  GetInvoiceDetailAsync — Kiểm tra quyền truy cập
    // ═══════════════════════════════════════════════════════════════

    [Fact]
    [Trait("Category", "Query")]
    public async Task GetInvoiceDetailAsync_WhenInvoiceDoesNotExist_ReturnsNull()
    {
        // Arrange: Repository không tìm thấy invoice
        var invoiceId = Guid.NewGuid();
        _mockInvoiceRepo.Setup(r => r.GetInvoiceWithDetailsAsync(invoiceId))
            .ReturnsAsync((Invoice?)null);

        // Act
        var result = await _sut.GetInvoiceDetailAsync(
            invoiceId, Guid.NewGuid(), Guid.NewGuid(), "CompanyAdmin");

        // Assert
        result.Should().BeNull("invoice không tồn tại phải trả về null");
    }

    [Fact]
    [Trait("Category", "Query")]
    public async Task GetInvoiceDetailAsync_WhenInvoiceIsDeleted_ReturnsNull()
    {
        // Arrange: Invoice đã bị soft-delete (status không phải Draft)
        var companyId = Guid.NewGuid();
        var invoice   = InvoiceTestFactory.CreateDeletedInvoice(companyId: companyId);
        invoice.Status = "Approved"; // Không phải Draft → không phải luồng merge

        _mockInvoiceRepo.Setup(r => r.GetInvoiceWithDetailsAsync(invoice.InvoiceId))
            .ReturnsAsync(invoice);

        // Act
        var result = await _sut.GetInvoiceDetailAsync(
            invoice.InvoiceId, companyId, Guid.NewGuid(), "CompanyAdmin");

        // Assert
        result.Should().BeNull("invoice đã xóa phải trả về null");
    }

    [Fact]
    [Trait("Category", "MultiTenancy")]
    public async Task GetInvoiceDetailAsync_WhenInvoiceBelongsToDifferentCompany_ReturnsNull()
    {
        // Arrange: Invoice thuộc company A, nhưng request từ company B
        var companyA   = Guid.NewGuid();
        var companyB   = Guid.NewGuid();
        var invoice    = InvoiceTestFactory.CreateDraftInvoice(companyId: companyA);

        _mockInvoiceRepo.Setup(r => r.GetInvoiceWithDetailsAsync(invoice.InvoiceId))
            .ReturnsAsync(invoice);

        // Act: Gọi với companyId = companyB (không phải chủ sở hữu)
        var result = await _sut.GetInvoiceDetailAsync(
            invoice.InvoiceId, companyB, Guid.NewGuid(), "CompanyAdmin");

        // Assert: Multi-tenant isolation phải ngăn chặn
        result.Should().BeNull("multi-tenant check phải ngăn company khác xem invoice");
    }

    [Fact]
    [Trait("Category", "RBAC")]
    public async Task GetInvoiceDetailAsync_WhenMemberAccessesOtherMembersInvoice_ReturnsNull()
    {
        // Arrange: Member A cố xem invoice do Member B upload
        var companyId   = Guid.NewGuid();
        var memberAId   = Guid.NewGuid();
        var memberBId   = Guid.NewGuid();
        var invoice     = InvoiceTestFactory.CreateDraftInvoice(
            companyId: companyId, uploadedBy: memberBId); // Upload bởi B

        _mockInvoiceRepo.Setup(r => r.GetInvoiceWithDetailsAsync(invoice.InvoiceId))
            .ReturnsAsync(invoice);

        // Act: Member A (memberAId) query invoice của Member B
        var result = await _sut.GetInvoiceDetailAsync(
            invoice.InvoiceId, companyId, memberAId, "Accountant");

        // Assert: RBAC phải ngăn Member xem invoice của người khác
        result.Should().BeNull("Member chỉ được xem invoice của chính mình");
    }

    [Fact]
    [Trait("Category", "RBAC")]
    public async Task GetInvoiceDetailAsync_WhenAdminAccessesAnyInvoice_ReturnsDto()
    {
        // Arrange: Admin xem invoice của bất kỳ member nào trong cùng company
        var companyId  = Guid.NewGuid();
        var memberId   = Guid.NewGuid();
        var adminId    = Guid.NewGuid();
        var invoice    = InvoiceTestFactory.CreateDraftInvoice(
            companyId: companyId, uploadedBy: memberId); // Upload bởi member

        _mockInvoiceRepo.Setup(r => r.GetInvoiceWithDetailsAsync(invoice.InvoiceId))
            .ReturnsAsync(invoice);

        // Act: Admin query — không bị RBAC chặn
        var result = await _sut.GetInvoiceDetailAsync(
            invoice.InvoiceId, companyId, adminId, "CompanyAdmin");

        // Assert
        result.Should().NotBeNull("Admin được xem mọi invoice trong company");
        result!.InvoiceId.Should().Be(invoice.InvoiceId);
        result.Status.Should().Be("Draft");
    }

    [Fact]
    [Trait("Category", "RBAC")]
    public async Task GetInvoiceDetailAsync_WhenMemberAccessesOwnInvoice_ReturnsDto()
    {
        // Arrange: Member xem invoice do chính mình upload
        var companyId = Guid.NewGuid();
        var memberId  = Guid.NewGuid();
        var invoice   = InvoiceTestFactory.CreateDraftInvoice(
            companyId: companyId, uploadedBy: memberId); // Upload bởi chính memberId

        _mockInvoiceRepo.Setup(r => r.GetInvoiceWithDetailsAsync(invoice.InvoiceId))
            .ReturnsAsync(invoice);

        // Act
        var result = await _sut.GetInvoiceDetailAsync(
            invoice.InvoiceId, companyId, memberId, "Accountant");

        // Assert
        result.Should().NotBeNull("Member được xem invoice của chính mình");
    }

    // ═══════════════════════════════════════════════════════════════
    //  SubmitInvoiceAsync — Workflow: Draft → Pending
    // ═══════════════════════════════════════════════════════════════

    [Fact]
    [Trait("Category", "Workflow")]
    public async Task SubmitInvoiceAsync_WhenInvoiceNotFound_ThrowsKeyNotFoundException()
    {
        // Arrange
        _mockInvoiceRepo.Setup(r => r.GetByIdAsync(It.IsAny<Guid>()))
            .ReturnsAsync((Invoice?)null);

        // Act & Assert: Phải ném đúng loại exception
        await _sut.Invoking(s => s.SubmitInvoiceAsync(
                Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
                "user@test.com", "CompanyAdmin", null, "127.0.0.1"))
            .Should().ThrowAsync<KeyNotFoundException>()
            .WithMessage("*Không tìm thấy*");
    }

    [Fact]
    [Trait("Category", "Workflow")]
    public async Task SubmitInvoiceAsync_WhenInvoiceBelongsToDifferentCompany_ThrowsUnauthorizedAccessException()
    {
        // Arrange
        var invoice   = InvoiceTestFactory.CreateDraftInvoice(companyId: Guid.NewGuid());
        var wrongComp = Guid.NewGuid(); // Company khác

        _mockInvoiceRepo.Setup(r => r.GetByIdAsync(invoice.InvoiceId))
            .ReturnsAsync(invoice);

        // Act & Assert
        await _sut.Invoking(s => s.SubmitInvoiceAsync(
                invoice.InvoiceId, wrongComp, Guid.NewGuid(),
                "user@test.com", "CompanyAdmin", null, null))
            .Should().ThrowAsync<UnauthorizedAccessException>();
    }

    [Fact]
    [Trait("Category", "Workflow")]
    public async Task SubmitInvoiceAsync_WhenInvoiceIsNotDraft_ThrowsInvalidOperationException()
    {
        // Arrange: Invoice đang ở trạng thái Pending (không phải Draft)
        var companyId = Guid.NewGuid();
        var invoice   = InvoiceTestFactory.CreatePendingInvoice(companyId: companyId);

        _mockInvoiceRepo.Setup(r => r.GetByIdAsync(invoice.InvoiceId))
            .ReturnsAsync(invoice);

        // Act & Assert
        await _sut.Invoking(s => s.SubmitInvoiceAsync(
                invoice.InvoiceId, companyId, Guid.NewGuid(),
                "user@test.com", "CompanyAdmin", null, null))
            .Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*Chỉ có thể gửi duyệt hóa đơn ở trạng thái Nháp*");
    }

    [Fact]
    [Trait("Category", "Workflow")]
    public async Task SubmitInvoiceAsync_WhenValidDraftInvoice_ChangesStatusToPending()
    {
        // Arrange: Happy path — invoice hợp lệ ở trạng thái Draft
        var companyId = Guid.NewGuid();
        var userId    = Guid.NewGuid();
        var invoice   = InvoiceTestFactory.CreateDraftInvoice(companyId: companyId);

        _mockInvoiceRepo.Setup(r => r.GetByIdAsync(invoice.InvoiceId))
            .ReturnsAsync(invoice);

        // Act
        await _sut.SubmitInvoiceAsync(
            invoice.InvoiceId, companyId, userId,
            "admin@company.com", "CompanyAdmin", "Gửi duyệt", "127.0.0.1");

        // Assert 1: Status đã chuyển sang Pending
        invoice.Status.Should().Be("Pending");

        // Assert 2: Workflow được cập nhật
        invoice.Workflow.SubmittedBy.Should().Be(userId);
        invoice.Workflow.SubmittedAt.Should().NotBeNull();
    }

    [Fact]
    [Trait("Category", "Workflow")]
    public async Task SubmitInvoiceAsync_WhenValidDraftInvoice_WritesAuditLog()
    {
        // Arrange
        var companyId = Guid.NewGuid();
        var userId    = Guid.NewGuid();
        var invoice   = InvoiceTestFactory.CreateDraftInvoice(companyId: companyId);

        _mockInvoiceRepo.Setup(r => r.GetByIdAsync(invoice.InvoiceId))
            .ReturnsAsync(invoice);

        // Act
        await _sut.SubmitInvoiceAsync(
            invoice.InvoiceId, companyId, userId,
            "admin@company.com", "CompanyAdmin", "Comment test", "192.168.1.1");

        // Assert: Audit log phải được ghi đúng action SUBMIT
        _mockAuditLogRepo.Verify(r => r.AddAsync(
            It.Is<InvoiceAuditLog>(log =>
                log.Action == "SUBMIT" &&
                log.InvoiceId == invoice.InvoiceId &&
                log.CompanyId == companyId &&
                log.UserId == userId
            )), Times.Once, "Audit log SUBMIT phải được tạo đúng 1 lần");
    }

    [Fact]
    [Trait("Category", "Workflow")]
    public async Task SubmitInvoiceAsync_WhenSuccessful_SendsNotificationToAdmins()
    {
        // Arrange
        var companyId = Guid.NewGuid();
        var invoice   = InvoiceTestFactory.CreateDraftInvoice(companyId: companyId);

        _mockInvoiceRepo.Setup(r => r.GetByIdAsync(invoice.InvoiceId))
            .ReturnsAsync(invoice);

        // Act
        await _sut.SubmitInvoiceAsync(
            invoice.InvoiceId, companyId, Guid.NewGuid(),
            "user@test.com", "CompanyAdmin", null, null);

        // Assert: Thông báo phải được gửi tới admin của company
        _mockNotification.Verify(n => n.SendNotificationToCompanyAdminsAsync(
            companyId,
            "Approval",
            It.IsAny<string>(),
            It.IsAny<string>(),
            It.IsAny<string?>(),
            It.IsAny<string?>(),
            invoice.InvoiceId,
            It.IsAny<string>()), Times.Once);
    }

    // ═══════════════════════════════════════════════════════════════
    //  ApproveInvoiceAsync — Workflow: Pending → Approved
    // ═══════════════════════════════════════════════════════════════

    [Fact]
    [Trait("Category", "Workflow")]
    public async Task ApproveInvoiceAsync_WhenInvoiceNotFound_ThrowsKeyNotFoundException()
    {
        // Arrange
        _mockInvoiceRepo.Setup(r => r.GetByIdAsync(It.IsAny<Guid>()))
            .ReturnsAsync((Invoice?)null);

        // Act & Assert
        await _sut.Invoking(s => s.ApproveInvoiceAsync(
                Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
                "admin@test.com", "CompanyAdmin", null, null))
            .Should().ThrowAsync<KeyNotFoundException>();
    }

    [Fact]
    [Trait("Category", "Workflow")]
    public async Task ApproveInvoiceAsync_WhenInvoiceIsNotPending_ThrowsInvalidOperationException()
    {
        // Arrange: Invoice ở trạng thái Draft (chưa được submit)
        var companyId = Guid.NewGuid();
        var invoice   = InvoiceTestFactory.CreateDraftInvoice(companyId: companyId);

        _mockInvoiceRepo.Setup(r => r.GetByIdAsync(invoice.InvoiceId))
            .ReturnsAsync(invoice);

        // Need AppDbContext mock — for ApproveInvoiceAsync's company lookup
        // Method sẽ throw trước khi đến phần query company vì status != "Pending"
        await _sut.Invoking(s => s.ApproveInvoiceAsync(
                invoice.InvoiceId, companyId, Guid.NewGuid(),
                "admin@test.com", "CompanyAdmin", null, null))
            .Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*Chỉ có thể duyệt hóa đơn*");
    }

    [Fact]
    [Trait("Category", "Workflow")]
    public async Task ApproveInvoiceAsync_WhenInvoiceBelongsToDifferentCompany_ThrowsUnauthorized()
    {
        // Arrange
        var invoice    = InvoiceTestFactory.CreatePendingInvoice(companyId: Guid.NewGuid());
        var wrongComp  = Guid.NewGuid();

        _mockInvoiceRepo.Setup(r => r.GetByIdAsync(invoice.InvoiceId))
            .ReturnsAsync(invoice);

        // Act & Assert
        await _sut.Invoking(s => s.ApproveInvoiceAsync(
                invoice.InvoiceId, wrongComp, Guid.NewGuid(),
                "admin@test.com", "CompanyAdmin", null, null))
            .Should().ThrowAsync<UnauthorizedAccessException>();
    }

    // ═══════════════════════════════════════════════════════════════
    //  RejectInvoiceAsync — Workflow: Pending → Rejected
    // ═══════════════════════════════════════════════════════════════

    [Fact]
    [Trait("Category", "Workflow")]
    public async Task RejectInvoiceAsync_WhenCalledByMember_ThrowsUnauthorizedAccessException()
    {
        // Arrange: Member không có quyền reject invoice
        // Act & Assert: Exception phải được ném TRƯỚC KHI query DB
        await _sut.Invoking(s => s.RejectInvoiceAsync(
                Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
                "member@test.com", "Accountant", // ← role không hợp lệ
                "Sai thông tin", null, null))
            .Should().ThrowAsync<UnauthorizedAccessException>()
            .WithMessage("*Kế toán trưởng và Admin*");
    }

    [Fact]
    [Trait("Category", "Workflow")]
    public async Task RejectInvoiceAsync_WhenInvoiceIsNotPending_ThrowsInvalidOperationException()
    {
        // Arrange: Invoice đang ở Draft (chưa được submit)
        var companyId = Guid.NewGuid();
        var invoice   = InvoiceTestFactory.CreateDraftInvoice(companyId: companyId);

        _mockInvoiceRepo.Setup(r => r.GetByIdAsync(invoice.InvoiceId))
            .ReturnsAsync(invoice);

        // Act & Assert
        await _sut.Invoking(s => s.RejectInvoiceAsync(
                invoice.InvoiceId, companyId, Guid.NewGuid(),
                "admin@test.com", "CompanyAdmin",
                "Lý do từ chối", null, null))
            .Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*Chỉ có thể từ chối hóa đơn*");
    }

    [Fact]
    [Trait("Category", "Workflow")]
    public async Task RejectInvoiceAsync_WhenValidPendingInvoice_ChangesStatusToRejectedAndLogsAudit()
    {
        // Arrange
        var companyId  = Guid.NewGuid();
        var adminId    = Guid.NewGuid();
        var invoice    = InvoiceTestFactory.CreatePendingInvoice(companyId: companyId);
        var reason     = "Sai thông tin người bán";

        _mockInvoiceRepo.Setup(r => r.GetByIdAsync(invoice.InvoiceId))
            .ReturnsAsync(invoice);

        // Act
        await _sut.RejectInvoiceAsync(
            invoice.InvoiceId, companyId, adminId,
            "admin@company.com", "CompanyAdmin",
            reason, "Comment", "127.0.0.1");

        // Assert 1: Status đổi sang Rejected
        invoice.Status.Should().Be("Rejected");
        invoice.Workflow.RejectedBy.Should().Be(adminId);
        invoice.Workflow.RejectionReason.Should().Be(reason);

        // Assert 2: Audit log với đúng action REJECT
        _mockAuditLogRepo.Verify(r => r.AddAsync(
            It.Is<InvoiceAuditLog>(log =>
                log.Action == "REJECT" &&
                log.Reason == reason &&
                log.InvoiceId == invoice.InvoiceId
            )), Times.Once);
    }

    // ═══════════════════════════════════════════════════════════════
    //  DeleteInvoiceAsync — Soft Delete: IsDeleted = true
    // ═══════════════════════════════════════════════════════════════

    [Fact]
    [Trait("Category", "Delete")]
    public async Task DeleteInvoiceAsync_WhenInvoiceNotFound_ReturnsFalse()
    {
        // Arrange
        _mockInvoiceRepo.Setup(r => r.GetByIdAsync(It.IsAny<Guid>()))
            .ReturnsAsync((Invoice?)null);

        // Act
        var result = await _sut.DeleteInvoiceAsync(
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
            "user@test.com", "CompanyAdmin", null);

        // Assert
        result.Should().BeFalse("invoice không tồn tại → phải trả về false");
    }

    [Fact]
    [Trait("Category", "Delete")]
    public async Task DeleteInvoiceAsync_WhenInvoiceBelongsToDifferentCompany_ReturnsFalse()
    {
        // Arrange: Multi-tenant isolation
        var invoice   = InvoiceTestFactory.CreateDraftInvoice(companyId: Guid.NewGuid());
        var wrongComp = Guid.NewGuid();

        _mockInvoiceRepo.Setup(r => r.GetByIdAsync(invoice.InvoiceId))
            .ReturnsAsync(invoice);

        // Act
        var result = await _sut.DeleteInvoiceAsync(
            invoice.InvoiceId, wrongComp, Guid.NewGuid(),
            "user@test.com", "CompanyAdmin", null);

        // Assert
        result.Should().BeFalse("company khác không được phép xóa invoice");
    }

    [Fact]
    [Trait("Category", "Delete")]
    public async Task DeleteInvoiceAsync_WhenValidInvoice_SetsIsDeletedTrue()
    {
        // Arrange
        var companyId = Guid.NewGuid();
        var invoice   = InvoiceTestFactory.CreateDraftInvoice(companyId: companyId);

        _mockInvoiceRepo.Setup(r => r.GetByIdAsync(invoice.InvoiceId))
            .ReturnsAsync(invoice);

        // Act
        var result = await _sut.DeleteInvoiceAsync(
            invoice.InvoiceId, companyId, Guid.NewGuid(),
            "admin@company.com", "CompanyAdmin", "127.0.0.1");

        // Assert 1: Method trả về true
        result.Should().BeTrue();

        // Assert 2: Soft delete đã được đánh dấu
        invoice.IsDeleted.Should().BeTrue();
        invoice.DeletedAt.Should().NotBeNull();
    }

    [Fact]
    [Trait("Category", "Delete")]
    public async Task DeleteInvoiceAsync_WhenValidInvoice_WritesTrashAuditLog()
    {
        // Arrange
        var companyId = Guid.NewGuid();
        var userId    = Guid.NewGuid();
        var invoice   = InvoiceTestFactory.CreateApprovedInvoice(companyId: companyId);

        _mockInvoiceRepo.Setup(r => r.GetByIdAsync(invoice.InvoiceId))
            .ReturnsAsync(invoice);

        // Act
        await _sut.DeleteInvoiceAsync(
            invoice.InvoiceId, companyId, userId,
            "admin@company.com", "CompanyAdmin", "10.0.0.1");

        // Assert: Audit log với action TRASH
        _mockAuditLogRepo.Verify(r => r.AddAsync(
            It.Is<InvoiceAuditLog>(log =>
                log.Action == "TRASH" &&
                log.InvoiceId == invoice.InvoiceId &&
                log.UserId == userId
            )), Times.Once, "Audit log TRASH phải được tạo khi chuyển vào thùng rác");
    }

    // ═══════════════════════════════════════════════════════════════
    //  RestoreInvoiceAsync — Khôi phục từ thùng rác
    // ═══════════════════════════════════════════════════════════════

    [Fact]
    [Trait("Category", "Delete")]
    public async Task RestoreInvoiceAsync_WhenInvoiceNotFoundInTrash_ReturnsFalse()
    {
        // Arrange
        _mockInvoiceRepo.Setup(r => r.GetTrashInvoiceWithDetailsAsync(It.IsAny<Guid>()))
            .ReturnsAsync((Invoice?)null);

        // Act
        var result = await _sut.RestoreInvoiceAsync(
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
            "admin@test.com", "CompanyAdmin", null);

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    [Trait("Category", "Delete")]
    public async Task RestoreInvoiceAsync_WhenValidDeletedInvoice_SetsIsDeletedFalse()
    {
        // Arrange
        var companyId = Guid.NewGuid();
        var invoice   = InvoiceTestFactory.CreateDeletedInvoice(companyId: companyId);

        _mockInvoiceRepo.Setup(r => r.GetTrashInvoiceWithDetailsAsync(invoice.InvoiceId))
            .ReturnsAsync(invoice);

        // Act
        var result = await _sut.RestoreInvoiceAsync(
            invoice.InvoiceId, companyId, Guid.NewGuid(),
            "admin@company.com", "CompanyAdmin", null);

        // Assert
        result.Should().BeTrue();
        invoice.IsDeleted.Should().BeFalse("invoice được khôi phục → IsDeleted = false");
        invoice.DeletedAt.Should().BeNull("DeletedAt phải được xoá");
    }

    [Fact]
    [Trait("Category", "Delete")]
    public async Task RestoreInvoiceAsync_WhenMemberRestoresOtherMembersInvoice_ReturnsFalse()
    {
        // Arrange: Member A cố khôi phục invoice của Member B
        var companyId = Guid.NewGuid();
        var memberA   = Guid.NewGuid();
        var memberB   = Guid.NewGuid();
        var invoice   = InvoiceTestFactory.CreateDeletedInvoice(companyId: companyId);
        invoice.Workflow = new InvoiceWorkflow { UploadedBy = memberB }; // Upload bởi B

        _mockInvoiceRepo.Setup(r => r.GetTrashInvoiceWithDetailsAsync(invoice.InvoiceId))
            .ReturnsAsync(invoice);

        // Act: Member A cố restore
        var result = await _sut.RestoreInvoiceAsync(
            invoice.InvoiceId, companyId, memberA, "memberA@test.com", "Accountant", null);

        // Assert: RBAC phải ngăn
        result.Should().BeFalse("Member chỉ được restore invoice của chính mình");
    }

    // ═══════════════════════════════════════════════════════════════
    //  GetInvoiceStatsAsync — Tính toán thống kê
    // ═══════════════════════════════════════════════════════════════

    [Fact]
    [Trait("Category", "Stats")]
    public async Task GetInvoiceStatsAsync_WithMixedStatuses_CalculatesCorrectCounts()
    {
        // Arrange: 5 invoices với trạng thái/risk levels khác nhau
        var companyId = Guid.NewGuid();
        var invoices = new List<Invoice>
        {
            // Approved invoices (hợp lệ)
            CreateInvoiceForStats(companyId, "Approved", "Green",    1_000_000m),
            CreateInvoiceForStats(companyId, "Approved", "Yellow",   2_000_000m),
            // Draft với RiskLevel = "Green" (cũng tính vào validCount)
            CreateInvoiceForStats(companyId, "Draft",    "Green",    500_000m),
            // Pending (cần xem xét)
            CreateInvoiceForStats(companyId, "Pending",  "Yellow",   3_000_000m),
            CreateInvoiceForStats(companyId, "Pending",  "Orange",   1_500_000m),
        };

        _mockInvoiceRepo.Setup(r => r.GetAllAsync())
            .ReturnsAsync(invoices);

        var startDate = DateTime.UtcNow.AddMonths(-1);
        var endDate   = DateTime.UtcNow.AddDays(1);

        // Act
        var stats = await _sut.GetInvoiceStatsAsync(startDate, endDate, null, companyId, Guid.NewGuid(), "Admin");

        // Assert
        stats.TotalCount.Should().Be(5,
            "có đúng 5 invoice hợp lệ (không xóa, không replaced)");

        // validCount = Approved hoặc RiskLevel=Green
        // → invoice 1 (Approved+Green), invoice 2 (Approved+Yellow), invoice 3 (Draft+Green)
        stats.ValidCount.Should().Be(3,
            "ValidCount = Approved(2) + RiskLevel='Green' nhưng chưa Approved(1)");

        // needReviewCount = chưa Approved VÀ (Yellow hoặc Orange)
        // → invoice 4 (Pending+Yellow), invoice 5 (Pending+Orange)
        stats.NeedReviewCount.Should().Be(2,
            "NeedReviewCount = Pending+Yellow(1) + Pending+Orange(1)");

        stats.ApprovedCount.Should().Be(2, "có 2 invoice status=Approved");

        // Tổng amount = 1M + 2M + 0.5M + 3M + 1.5M = 8M
        stats.TotalAmount.Should().Be(8_000_000m);
    }

    [Fact]
    [Trait("Category", "Stats")]
    public async Task GetInvoiceStatsAsync_WithStatusFilter_ReturnsOnlyMatchingInvoices()
    {
        // Arrange: Filter chỉ lấy Approved invoices
        var companyId = Guid.NewGuid();
        var invoices  = new List<Invoice>
        {
            CreateInvoiceForStats(companyId, "Approved", "Green",  1_000_000m),
            CreateInvoiceForStats(companyId, "Pending",  "Yellow", 2_000_000m),
            CreateInvoiceForStats(companyId, "Draft",    "Green",  500_000m),
        };

        _mockInvoiceRepo.Setup(r => r.GetAllAsync())
            .ReturnsAsync(invoices);

        // Act: Filter theo status = "Approved"
        var stats = await _sut.GetInvoiceStatsAsync(
            DateTime.UtcNow.AddMonths(-1), DateTime.UtcNow.AddDays(1),
            "Approved", companyId, Guid.NewGuid(), "Admin");

        // Assert: Chỉ trả về 1 invoice Approved
        stats.TotalCount.Should().Be(1);
        stats.TotalAmount.Should().Be(1_000_000m);
    }

    [Fact]
    [Trait("Category", "Stats")]
    public async Task GetInvoiceStatsAsync_WithDateFilter_ExcludesOutOfRangeInvoices()
    {
        // Arrange: Invoice nằm ngoài date range
        var companyId     = Guid.NewGuid();
        var oldInvoiceDate = DateTime.UtcNow.AddYears(-2);
        var recentDate    = DateTime.UtcNow;

        var invoices = new List<Invoice>
        {
            CreateInvoiceForStats(companyId, "Approved", "Green", 1_000_000m, oldInvoiceDate),  // Ngoài range
            CreateInvoiceForStats(companyId, "Approved", "Green", 2_000_000m, recentDate),       // Trong range
        };

        _mockInvoiceRepo.Setup(r => r.GetAllAsync())
            .ReturnsAsync(invoices);

        // Act: Query chỉ từ 1 năm trước đến nay
        var startDate = DateTime.UtcNow.AddYears(-1);
        var endDate   = DateTime.UtcNow.AddDays(1);
        var stats = await _sut.GetInvoiceStatsAsync(startDate, endDate, null, companyId, Guid.NewGuid(), "Admin");

        // Assert: Chỉ trả về invoice gần đây
        stats.TotalCount.Should().Be(1);
        stats.TotalAmount.Should().Be(2_000_000m);
    }

    // ═══════════════════════════════════════════════════════════════
    //  helper: tạo invoice nhanh cho stats testing
    // ═══════════════════════════════════════════════════════════════

    private static Invoice CreateInvoiceForStats(
        Guid companyId, string status, string riskLevel,
        decimal totalAmount, DateTime? dateOverride = null)
    {
        return new Invoice
        {
            InvoiceId     = Guid.NewGuid(),
            CompanyId     = companyId,
            InvoiceNumber = $"INV-{Guid.NewGuid():N}",
            Status        = status,
            RiskLevel     = riskLevel,
            TotalAmount   = totalAmount,
            InvoiceDate   = dateOverride ?? DateTime.UtcNow,
            IsDeleted     = false,
            IsReplaced    = false,
            Seller        = new SellerInfo(),
            Buyer         = new BuyerInfo(),
            Workflow      = new InvoiceWorkflow()
        };
    }
}
