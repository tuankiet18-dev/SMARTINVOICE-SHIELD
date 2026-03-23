from huggingface_hub import snapshot_download

snapshot_download(
    repo_id="microsoft/layoutlmv3-base",
    local_dir="layoutlmv3-base",
    local_dir_use_symlinks=False
)

print("Download completed.")
