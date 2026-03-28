// pages/LoginPage.jsx
import api from '../services/api';
import { useNavigate } from 'react-router-dom';

function LoginPage() {
  const navigate = useNavigate();

  const handleLogin = async (e) => {
    e.preventDefault();
    // email and password should be defined or from state
    const { data } = await api.post('/auth/login', { email, password });
    localStorage.setItem('token', data.token);
    navigate('/boards');
  };

}

export default LoginPage;