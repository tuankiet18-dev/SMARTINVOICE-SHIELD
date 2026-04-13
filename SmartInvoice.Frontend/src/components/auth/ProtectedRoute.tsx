import React from 'react';
import { Navigate, Outlet, useLocation } from 'react-router-dom';
import { useAuth } from '@/contexts/AuthContext';

interface ProtectedRouteProps {
  allowedRoles?: string[];
  requiredPermission?: string; // Thêm dòng này để check quyền
}

const ProtectedRoute: React.FC<ProtectedRouteProps> = ({ allowedRoles, requiredPermission }) => {
  const { user } = useAuth();
  const location = useLocation();

  // 1. Chưa đăng nhập thì đuổi ra Login
  if (!user) {
    return <Navigate to="/login" state={{ from: location }} replace />;
  }

  // 2. Kiểm tra Role (Nếu truyền allowedRoles mà user không có trong mảng đó)
  if (allowedRoles && !allowedRoles.includes(user.role)) {
    return <Navigate to="/app/invoices" replace />; 
  }

  // 3. Kiểm tra Permission cụ thể (Ví dụ: truyền vào "dashboard:view")
  if (requiredPermission && !user.permissions?.includes(requiredPermission)) {
    // Nếu Accountant cố tình gõ URL /app/dashboard, đá họ về trang Hóa đơn
    return <Navigate to="/app/invoices" replace />;
  }

  // Hợp lệ hết thì cho đi tiếp vào các Route con
  return <Outlet />;
};

export default ProtectedRoute;