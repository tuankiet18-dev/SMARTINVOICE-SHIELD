FROM nvidia/cuda:11.8.0-cudnn8-runtime-ubuntu22.04

# Prevent interactive prompts during apt-get
ENV DEBIAN_FRONTEND=noninteractive

WORKDIR /app

# Install Python 3.10, pip, and system dependencies (OpenCV, pdf2image)
RUN apt-get update && apt-get install -y \
    python3.10 \
    python3.10-dev \
    python3-pip \
    libgl1 \
    libglib2.0-0 \
    poppler-utils \
    && ln -sf /usr/bin/python3.10 /usr/bin/python \
    && ln -sf /usr/bin/pip3 /usr/bin/pip \
    && rm -rf /var/lib/apt/lists/*

# Install PyTorch with CUDA 11.8
RUN pip install --no-cache-dir torch==2.1.2 torchvision==0.16.2 --index-url https://download.pytorch.org/whl/cu118

# Install PaddlePaddle GPU (CUDA 11.8 compatible)
RUN pip install --no-cache-dir paddlepaddle-gpu==2.6.1

# Install other requirements
COPY requirements-docker.txt .
RUN pip install --no-cache-dir -r requirements-docker.txt

# Copy source code
COPY . /app

# Expose port
EXPOSE 5000

# Set environment variables
ENV HOST=0.0.0.0
ENV PORT=5000
ENV DEVICE=cuda
ENV PYTHONUNBUFFERED=1

# Change directory to the web_app folder where app.py lives
WORKDIR /app/inference/web_app

# Run the Flask app
CMD ["python", "app.py"]
