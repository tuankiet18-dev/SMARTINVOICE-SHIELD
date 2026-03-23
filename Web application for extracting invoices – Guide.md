# 🧾 Invoice Extraction Web App – Hướng dẫn chạy

## 📦 1. Chuẩn bị model

Bạn cần **copy toàn bộ thư mục models** sang máy của bạn.

Ví dụ cấu trúc:

```
OCR_MODELS/
└── model_final_layoutlmv3/
    ├── model_final_header/
    │   └── final_model/
    ├── model_final_table/
    │   └── final_model/
    └── model_final_footer/
        └── final_model/
```

---

## ⚙️ 2. Set Environment Variables

Mở PowerShell và chạy:

```powershell
$env:MODEL_PATH_HEADER="C:\path\to\OCR_MODELS\model_final_layoutlmv3\model_final_header\final_model"
$env:MODEL_PATH_TABLE="C:\path\to\OCR_MODELS\model_final_layoutlmv3\model_final_table\final_model"
$env:MODEL_PATH_FOOTER="C:\path\to\OCR_MODELS\model_final_layoutlmv3\model_final_footer\final_model"
```

👉 Thay `C:\path\to\...` bằng đường dẫn thực tế trên máy bạn

---

## 📁 3. Di chuyển tới thư mục web app

```powershell
cd invoice_ocr/inference/web_app
```

---

## ▶️ 4. Chạy ứng dụng

```bash
python app.py
```

---

## 🌐 5. Mở giao diện web

Truy cập:

```
http://localhost:5000/
```

---

## 📤 6. Chạy inference

1. Upload file hóa đơn
2. Nhấn nút **Extract**
3. Đợi xử lý

---

## 📋 7. Lấy kết quả JSON

👉 Sau khi chạy xong:

* Nhấn nút **Copy JSON**
* Paste ra file `.json` nếu cần

---

## ⚠️ Lưu ý

* Model **không nằm trong GitHub repo**
* Phải copy model thủ công hoặc download từ nơi được cung cấp
* Nếu lỗi model → kiểm tra lại:

  * Đường dẫn ENV
  * Folder có chứa `config.json` và `pytorch_model.bin`

---

## ✅ Checklist nhanh

* [ ] Đã copy models
* [ ] Đã set ENV
* [ ] Đã cd đúng folder
* [ ] App chạy không lỗi
* [ ] Truy cập được localhost:5000

---

🔥 Done! Giờ bạn có thể chạy inference full pipeline (header + table + footer)
