# SmartInvoice Shield 🛡️

**SmartInvoice Shield** is an intelligent invoice management solution, integrating AI to automate data extraction (OCR), verify legal compliance, and manage business quotas. The system is designed with a modern architecture and high scalability on the AWS platform.

---

## 🚀 Key Features

- **AI-Powered Data Extraction (OCR)**: Automatically extract information from invoices (PDF/Images) using a triple-engine approach: Gemini Vision API, PaddleOCR, and VietOCR.
- **Legal Validation**: Verify digital signatures and the integrity of XML invoice files according to General Department of Taxation standards.
- **Quota Management**: Manage service packages, limiting the number of invoices and storage capacity for each company.
- **User Permissions**: Integrate AWS Cognito for Identity and Access Management (IAM) with granular Role-Based Access Control (RBAC).
- **Invoice Approval Workflow**: Handle processes from Draft -> Pending -> Approved/Rejected with detailed audit logs.
- **VietQR Integration**: Automatically verify business information via Tax Code.

---

## 🛠️ Technology Stack

### Backend
- **Core**: .NET 9 Web API
- **Database**: PostgreSQL with Entity Framework Core
- **Messaging**: AWS SQS (Simple Queue Service)
- **Identity**: AWS Cognito
- **Storage**: AWS S3
- **Resilience**: Polly (Retry, Circuit Breaker)

### Frontend
- **Core**: React + TypeScript
- **Build Tool**: Vite
- **Styling**: Tailwind CSS / Shadcn UI
- **Deployment**: AWS Amplify

### AI Service (OCR)
- **Engine**: Python 3.10+
- **OCR**: PaddleOCR, VietOCR
- **LLM**: Google Gemini Flash 1.5 (for structured data extraction)
- **API**: FastAPI / Flask

---

## 🏗️ System Architecture

The system operates on a **Sequential Processing** mechanism to ensure stability and resource optimization:

1. **Frontend** uploads the invoice to the **Backend**.
2. **Backend** stores the file in **S3** and sends a message to **SQS**.
3. **Background Worker** (.NET Managed Service) polls messages from SQS and calls the **AI Service**.
4. **AI Service** performs OCR and returns results to update the database.

---

## 💻 Local Development Guide

### System Requirements
- Docker & Docker Compose
- .NET 9 SDK (if running without Docker)
- Node.js 18+

### Steps to Run

1. **Clone the repository**:
   ```bash
   git clone https://github.com/tuankiet18-dev/SMARTINVOICE-SHIELD.git
   cd SMARTINVOICE-SHIELD
   ```

2. **Configure environment**:
   - Backend: Create a `.env` file in the `SmartInvoice.API` directory with AWS information (Access Key, Cognito, SQS, S3).
   - Frontend: Verify `VITE_API_URL` in `docker-compose.yml`.

3. **Launch with Docker Compose**:
   ```bash
   docker-compose up -d --build
   ```

4. **Access**:
   - **Frontend**: [http://localhost:3000](http://localhost:3000)
   - **Swagger API**: [http://localhost:5172/swagger](http://localhost:5172/swagger)

---

## ☁️ Cloud Deployment (CI/CD)

The project includes built-in **GitHub Actions** for automated deployment:

- **Backend**: Deployed to **AWS Elastic Beanstalk (EBS)**.
- **AI Service**: Deployed as a container to **AWS ECS Fargate**.
- **Frontend**: Deployed via **AWS Amplify**.

Setting up CI/CD requires configuring the following **GitHub Secrets**:
- `AWS_ACCESS_KEY_ID`
- `AWS_SECRET_ACCESS_KEY`
- `AWS_REGION`
- `AWS_ACCOUNT_ID`

---

## 👥 Authors

This project is developed and maintained by the **SmartInvoice Shield Team**:

- [Tuấn Kiệt](https://github.com/tuankiet18-dev)
- [Nhật Anh](https://github.com/nhatanh-dev)
- [Philipsgn](https://github.com/philipsgn)
- [QuanPM77](https://github.com/QuanPM77)
