import axios from 'axios';

const BASE_URL = process.env.REACT_APP_API_BASE_URL || window.location.origin;

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
    const token = localStorage.getItem('token');
    if (error.response?.status === 401 && token) {
      localStorage.removeItem('token');
      window.location.href = '/';
    }
    return Promise.reject(error);
  }
);

export default api;