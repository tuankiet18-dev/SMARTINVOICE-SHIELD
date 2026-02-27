import axios from 'axios';

const API_URL = import.meta.env.VITE_API_URL || 'http://localhost:5172/api';

export const apiClient = axios.create({
    baseURL: API_URL,
    withCredentials: true,
    headers: {
        'Content-Type': 'application/json',
    },
});

apiClient.interceptors.request.use(
    (config) => {
        const token = localStorage.getItem('token');
        if (token) {
            config.headers.Authorization = `Bearer ${token}`;
        }
        return config;
    },
    (error) => Promise.reject(error)
);

apiClient.interceptors.response.use(
    (response) => response,
    async (error) => {
        const originalRequest = error.config;
        if (error.response?.status === 401 && !originalRequest._retry) {
            originalRequest._retry = true;
            try {
                const userStr = localStorage.getItem('user');
                if (userStr) {
                    const user = JSON.parse(userStr);
                    const res = await axios.post(`${API_URL}/auth/refresh-token`, {
                        email: user.email
                    }, { withCredentials: true });
                    const data = res.data;
                    localStorage.setItem('token', data.accessToken);
                    localStorage.setItem('idToken', data.idToken);
                    localStorage.setItem('user', JSON.stringify(data.user));

                    originalRequest.headers.Authorization = `Bearer ${data.accessToken}`;
                    return apiClient(originalRequest);
                }
            } catch (err) {
                // Refresh failed, logout
                localStorage.removeItem('token');
                localStorage.removeItem('idToken');
                localStorage.removeItem('user');
                window.location.href = '/login';
            }
        }
        return Promise.reject(error);
    }
);
