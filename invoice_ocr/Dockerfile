FROM python:3.10-slim

# Prevent interactive prompts during apt-get
ENV DEBIAN_FRONTEND=noninteractive

WORKDIR /app

# Install system dependencies (OpenCV, pdf2image)
RUN apt-get update && apt-get install -y \
    libgl1 \
    libglib2.0-0 \
    poppler-utils \
    && rm -rf /var/lib/apt/lists/*

# Install PyTorch (CPU version)
RUN pip install --no-cache-dir torch==2.1.2 torchvision==0.16.2 --index-url https://download.pytorch.org/whl/cpu

# Install PaddlePaddle (CPU version)
RUN pip install --no-cache-dir paddlepaddle==2.6.2

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
ENV DEVICE=cpu
ENV PYTHONUNBUFFERED=1

# Change directory to the web_app folder where app.py lives
WORKDIR /app/inference/web_app

# Run the app with Gunicorn (production WSGI server)
# Use threads instead of multiple workers to save RAM on Fargate
CMD ["gunicorn", "--workers", "1", "--threads", "4", "--timeout", "300", "--bind", "0.0.0.0:5000", "app:app"]
