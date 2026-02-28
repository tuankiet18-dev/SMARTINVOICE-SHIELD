import { ConfigProvider } from 'antd';
import viVN from 'antd/locale/vi_VN';
import { BrowserRouter, Routes, Route, Navigate } from 'react-router-dom';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import theme from './theme/antdTheme';
import AppLayout from './layouts/AppLayout';
import LandingPage from './pages/LandingPage';
import Login from './pages/Login';
import Register from './pages/Register';
import Dashboard from './pages/Dashboard';
import InvoiceList from './pages/InvoiceList';
import UploadInvoice from './pages/UploadInvoice';
import ValidationPage from './pages/ValidationPage';
import ReportsPage from './pages/ReportsPage';
import AuditLogPage from './pages/AuditLogPage';
import NotFound from './pages/NotFound';
import ApprovalDashboard from './pages/ApprovalDashboard';
import TeamManagement from './pages/TeamManagement';
import TenantManagement from './pages/TenantManagement';
import GlobalBlacklist from './pages/GlobalBlacklist';
import SystemConfig from './pages/SystemConfig';

const queryClient = new QueryClient();

const App = () => (
  <QueryClientProvider client={queryClient}>
    <ConfigProvider theme={theme} locale={viVN}>
      <BrowserRouter>
        <Routes>
          <Route path="/" element={<LandingPage />} />
          <Route path="/login" element={<Login />} />
          <Route path="/register" element={<Register />} />
          <Route path="/app" element={<AppLayout />}>
            <Route index element={<Navigate to="/app/dashboard" replace />} />
            <Route path="dashboard" element={<Dashboard />} />
            <Route path="invoices" element={<InvoiceList />} />
            <Route path="upload" element={<UploadInvoice />} />
            <Route path="validation" element={<ValidationPage />} />
            <Route path="reports" element={<ReportsPage />} />
            <Route path="audit-log" element={<AuditLogPage />} />
            <Route path="approval-dashboard" element={<ApprovalDashboard />} />
            <Route path="team" element={<TeamManagement />} />
            <Route path="tenants" element={<TenantManagement />} />
            <Route path="global-blacklist" element={<GlobalBlacklist />} />
            <Route path="system-config" element={<SystemConfig />} />
          </Route>
          <Route path="*" element={<NotFound />} />
        </Routes>
      </BrowserRouter>
    </ConfigProvider>
  </QueryClientProvider>
);

export default App;
