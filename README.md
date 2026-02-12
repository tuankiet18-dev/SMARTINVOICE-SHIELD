# Hướng dẫn chạy dự án Smart Invoice với Docker

## Yêu cầu
- Docker & Docker Compose đã được cài đặt.

## Cách chạy

1. **Dừng các container cũ (nếu có):**
   ```bash
   docker-compose down
   ```

2. **Build và khởi chạy Docker:**
   ```bash
   docker-compose up -d --build
   ```

3. **Chờ khoảng 10 giây để Backend khởi động hoàn tất.**

## Truy cập ứng dụng
- **Swagger UI**: [http://localhost:5172/swagger](http://localhost:5172/swagger)

## Xem logs (Kiểm tra lỗi nếu có)
Để xem logs realtime của các container đang chạy:
```bash
docker-compose logs -f
```
