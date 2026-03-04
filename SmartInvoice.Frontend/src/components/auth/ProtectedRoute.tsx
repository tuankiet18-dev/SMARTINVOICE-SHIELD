import React from 'react';
import { Navigate, Outlet, useLocation } from 'react-router-dom';
import { useAuth } from '@/contexts/AuthContext';
import { Spin } from 'antd';

interface ProtectedRouteProps {
    allowedRoles?: string[];
}

const ProtectedRoute: React.FC<ProtectedRouteProps> = ({ allowedRoles }) => {
    const { isAuthenticated, isLoading, user } = useAuth();
    const location = useLocation();

    // Show loading spinner while checking auth status (especially on refresh)
    if (isLoading) {
        return (
            <div style={{ height: '100vh', display: 'flex', justifyContent: 'center', alignItems: 'center' }}>
                <Spin size="large" tip="Đang kiểm tra quyền truy cập..." />
            </div>
        );
    }

    if (!isAuthenticated) {
        // Redirect them to the /login page, but save the current location they were trying to go to
        return <Navigate to="/login" state={{ from: location }} replace />;
    }

    if (allowedRoles && allowedRoles.length > 0 && user) {
        if (!allowedRoles.includes(user.role)) {
            // Role not authorized
            // You can redirect to an unauthorized page or dashboard
            return <Navigate to="/app/dashboard" replace />;
        }
    }

    return <Outlet />;
};

export default ProtectedRoute;
