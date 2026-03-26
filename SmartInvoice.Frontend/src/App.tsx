import { ConfigProvider, App as AntdApp } from 'antd';
import viVN from 'antd/locale/vi_VN';
import { BrowserRouter, Routes, Route, Navigate } from 'react-router-dom';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import theme from './theme/antdTheme';
import { AuthProvider } from './contexts/AuthContext';
import ProtectedRoute from './components/auth/ProtectedRoute';
import AppLayout from './layouts/AppLayout';
import SuperAdminLayout from './layouts/SuperAdminLayout';
import LandingPage from './pages/LandingPage';
import Login from './pages/Login';
import Register from './pages/Register';
import TermsOfUse from './pages/TermsOfUse';
import PrivacyPolicy from './pages/PrivacyPolicy';
import Dashboard from './pages/Dashboard';
import InvoiceList from './pages/InvoiceList';
import InvoiceDetail from './pages/InvoiceDetail';
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
import Profile from './pages/Profile';
import SubscriptionPage from './pages/SubscriptionPage';
import PaymentResult from './pages/PaymentResult';
import Settings from './pages/Settings';
import TrashInvoiceList from './pages/TrashInvoiceList';

const queryClient = new QueryClient();

const App = () => (
  <QueryClientProvider client={queryClient}>
    <ConfigProvider theme={theme} locale={viVN}>
      <AntdApp>
        <AuthProvider>
          <BrowserRouter future={{ v7_startTransition: true, v7_relativeSplatPath: true }}>
            <Routes>
              <Route path="/" element={<LandingPage />} />
              <Route path="/login" element={<Login />} />
              <Route path="/register" element={<Register />} />
              <Route path="/terms-of-use" element={<TermsOfUse />} />
              <Route path="/privacy-policy" element={<PrivacyPolicy />} />
              {/* Company Routes (Member & CompanyAdmin only) */}
              <Route path="/app" element={<ProtectedRoute allowedRoles={['Member', 'CompanyAdmin']} />}>
                <Route element={<AppLayout />}>
                  <Route index element={<Navigate to="/app/dashboard" replace />} />
                  <Route path="dashboard" element={<Dashboard />} />
                  <Route path="invoices" element={<InvoiceList />} />
                  <Route path="invoices/:id" element={<InvoiceDetail />} />
                  <Route path="upload" element={<UploadInvoice />} />
                  <Route path="validation" element={<ValidationPage />} />
                  <Route path="reports" element={<ReportsPage />} />
                  <Route path="profile" element={<Profile />} />
                  <Route path="subscription" element={<SubscriptionPage />} />
                  <Route path="payment/result" element={<PaymentResult />} />
                  <Route path="settings" element={<Settings />} />
                  <Route path="trash" element={<TrashInvoiceList />} />

                  {/* CompanyAdmin Only Routes */}
                  <Route element={<ProtectedRoute allowedRoles={['CompanyAdmin']} />}>
                    <Route path="approval-dashboard" element={<ApprovalDashboard />} />
                    <Route path="team" element={<TeamManagement />} />
                    <Route path="audit-log" element={<AuditLogPage />} />
                  </Route>
                </Route>
              </Route>

              {/* SuperAdmin Routes */}
              <Route path="/admin" element={<ProtectedRoute allowedRoles={['SuperAdmin']} />}>
                <Route element={<SuperAdminLayout />}>
                  <Route index element={<Navigate to="/admin/tenants" replace />} />
                  <Route path="tenants" element={<TenantManagement />} />
                  <Route path="global-blacklist" element={<GlobalBlacklist />} />
                  <Route path="system-config" element={<SystemConfig />} />
                  <Route path="settings" element={<Settings />} />
                  <Route path="profile" element={<Profile />} />
                </Route>
              </Route>
              <Route path="*" element={<NotFound />} />
            </Routes>
          </BrowserRouter>
        </AuthProvider>
      </AntdApp>
    </ConfigProvider>
  </QueryClientProvider>
);

export default App;
