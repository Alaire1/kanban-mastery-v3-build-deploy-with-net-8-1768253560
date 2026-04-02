import axios from 'axios';

const BASE_URL = 'http://localhost:5000/';

const api = axios.create({
  baseURL: BASE_URL
});

// ✅ Already have this
api.interceptors.request.use(config => {
  const token = localStorage.getItem('token');
  if (token) config.headers.Authorization = `Bearer ${token}`;
  return config;
});


api.interceptors.response.use(
  (response) => response,
  (error) => {
    // Only redirect on 401 if a token exists (user is logged in)
    const token = localStorage.getItem('token');
    if (error.response?.status === 401 && token) {
      localStorage.removeItem('token');
      window.location.href = '/';
    }
    // Otherwise, let the error propagate (e.g., login failure)
    return Promise.reject(error);
  }
);

export default api;