import React, { createContext, useContext, useState, useEffect } from 'react';
import { authService, LoginRequest } from '@/services/auth';
import { message } from 'antd';

export interface User {
    id: string;
    email: string;
    fullName: string;
    role: string;
    companyId: string;
    employeeId?: string;
    permissions?: string[];
    isActive?: boolean;
    companyName?: string;
}

interface AuthContextType {
    user: User | null;
    isAuthenticated: boolean;
    isLoading: boolean;
    login: (values: LoginRequest) => Promise<any>;
    logout: () => void;
    checkAuth: () => void;
}

const AuthContext = createContext<AuthContextType | undefined>(undefined);

export const AuthProvider: React.FC<{ children: React.ReactNode }> = ({ children }) => {
    const [user, setUser] = useState<User | null>(null);
    const [isLoading, setIsLoading] = useState<boolean>(true);

    const checkAuth = () => {
        setIsLoading(true);
        try {
            const token = localStorage.getItem('token');
            const storedUser = localStorage.getItem('user');

            if (token && storedUser) {
                setUser(JSON.parse(storedUser));
            } else {
                setUser(null);
            }
        } catch (error) {
            console.error('Lỗi khi parse user từ localStorage:', error);
            setUser(null);
        } finally {
            setIsLoading(false);
        }
    };

    useEffect(() => {
        checkAuth();

        // Listen to local storage changes for multi-tab sync (e.g., logout in another tab)
        const handleStorageChange = (e: StorageEvent) => {
            if (e.key === 'token' || e.key === 'user' || e.key === null) {
                checkAuth();
            }
        };
        window.addEventListener('storage', handleStorageChange);
        return () => window.removeEventListener('storage', handleStorageChange);
    }, []);

    const login = async (values: LoginRequest) => {
        const data = await authService.login(values);

        // Return immediately if it requires new password challenge
        if (data.challengeName === 'NEW_PASSWORD_REQUIRED' && data.session) {
            return data;
        }

        if (data.accessToken && data.user) {
            localStorage.setItem('token', data.accessToken);
            localStorage.setItem('idToken', data.idToken);
            localStorage.setItem('user', JSON.stringify(data.user));
            setUser(data.user);
        }
        return data;
    };

    const logout = async () => {
        try {
            setIsLoading(true);
            await authService.logout();
        } catch (error) {
            console.error("Lỗi khi đăng xuất:", error);
        } finally {
            setUser(null);
            setIsLoading(false);
        }
    };

    const value = {
        user,
        isAuthenticated: !!user,
        isLoading,
        login,
        logout,
        checkAuth
    };

    return <AuthContext.Provider value={value}>{children}</AuthContext.Provider>;
};

export const useAuth = () => {
    const context = useContext(AuthContext);
    if (context === undefined) {
        throw new Error('useAuth must be used within an AuthProvider');
    }
    return context;
};
