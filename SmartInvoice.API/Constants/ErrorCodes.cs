namespace SmartInvoice.API.Constants;

public static class ErrorCodes
{
    // XML Structure
    public const string XmlMissingField = "ERR_XML_MISSING_FIELD";
    public const string DataNotNumber = "ERR_DATA_NOT_NUMBER";
    public const string XmlStruct = "ERR_XML_STRUCT";
    public const string XmlSys = "ERR_XML_SYS";

    // OCR
    public const string OcrEmpty = "ERR_OCR_EMPTY";

    // Signatures
    public const string SigMissing = "ERR_SIG_MISSING";
    public const string SigExpired = "ERR_SIG_EXPIRED";
    public const string SigInvalid = "ERR_SIG_INVALID";
    public const string SigSys = "ERR_SIG_SYS";

    // Logic constraints
    public const string LogicTotalMismatch = "ERR_LOGIC_TOTAL_MISMATCH";
    public const string LogicTaxFormat = "ERR_LOGIC_TAX_FORMAT";
    public const string LogicSystem = "ERR_LOGIC_SYS";
    public const string LogicOwner = "ERR_LOGIC_OWNER";
    public const string LogicDuplicateRejected = "ERR_LOGIC_DUP_REJECTED";
    public const string LogicDuplicate = "ERR_LOGIC_DUP";
    public const string LogicBlacklist = "ERR_LOGIC_BLACKLIST";
    public const string LogicNoItems = "ERR_LOGIC_NO_ITEMS";
    public const string LogicTaxRate = "ERR_LOGIC_TAX_RATE";
    public const string LogicNoProperty = "ERR_LOGIC_NO_PROPERTY";
    public const string LogicSalesTotalMismatch = "ERR_LOGIC_SALES_TOTAL_MISMATCH";
    public const string LogicVersion = "ERR_LOGIC_VERSION";
    public const string LogicInvType = "ERR_LOGIC_INV_TYPE";
    public const string LogicInvSymbol = "ERR_LOGIC_INV_SYMBOL";
    public const string LogicInvNum = "ERR_LOGIC_INV_NUM";
    public const string LogicCurrency = "ERR_LOGIC_CURRENCY";
    public const string LogicExRate = "ERR_LOGIC_EX_RATE";
    public const string LogicRecordType = "ERR_LOGIC_REL";
    public const string LogicMccqt = "ERR_LOGIC_MCCQT";
    public const string LogicSignerMismatch = "ERR_LOGIC_SIGNER_MISMATCH";

    public const string Cancelled = "ERR_CANCELLED";
    public const string ExtractData = "ERR_EXTRACT_DATA";
    public const string Unknown = "ERR_UNKNOWN";
}
