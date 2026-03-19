import os
import zipfile
from pathlib import Path

BASE_DIR = Path(__file__).resolve().parent
DEFAULT_SOURCE_DIR = BASE_DIR / "synthetic_invoices" / "output" / "hf_dataset_footer"
DEFAULT_ZIP_NAME = BASE_DIR / "synthetic_invoices" / "output" / "hf_dataset_footer.zip"

SOURCE_DIR = Path(os.getenv("ZIP_SOURCE_DIR", DEFAULT_SOURCE_DIR)).expanduser().resolve()
ZIP_NAME = Path(os.getenv("ZIP_NAME", DEFAULT_ZIP_NAME)).expanduser().resolve()

SOURCE_DIR.mkdir(parents=True, exist_ok=True)
ZIP_NAME.parent.mkdir(parents=True, exist_ok=True)

with zipfile.ZipFile(ZIP_NAME, "w", zipfile.ZIP_DEFLATED) as zipf:
    for root, dirs, files in os.walk(SOURCE_DIR):
        for file in files:
            full_path = os.path.join(root, file)
            relative_path = os.path.relpath(full_path, SOURCE_DIR)

            # Force forward slash
            relative_path = relative_path.replace("\\", "/")

            zipf.write(full_path, os.path.join("hf_dataset", relative_path))

print(f"Zip created successfully: {ZIP_NAME}")
