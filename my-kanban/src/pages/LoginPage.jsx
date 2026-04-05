import { useState } from 'react';
import { useNavigate, Link } from 'react-router-dom';
import api from '../services/api';
import { useFormValidation } from '../hooks/useFormValidation';
import { ROUTES } from '../constants/routes';


function LoginPage() {
  const [email, setEmail] = useState('');
  const [password, setPassword] = useState('');
  const [apiError, setApiError] = useState('');
  const [loading, setLoading] = useState(false);
  const navigate = useNavigate();

  const { validateAll, touchField, getFieldError } = useFormValidation(['email', 'password']);

  const handleLogin = async (e) => {
    e.preventDefault();
    setApiError('');
    const values = { email, password };
    if (!validateAll(values)) return; // stop if invalid
    setLoading(true);
    try {
      const response = await api.post('/login', values);
      localStorage.setItem('token', response.data.accessToken);
      navigate(ROUTES.DASHBOARD);
    } catch (error) {
      setApiError(error.response?.data?.message || 'Login failed');
    } finally {
      setLoading(false);
    }
  };

  const inputClass = (field) =>
    `w-full border rounded-xl px-4 py-2.5 text-sm text-green-900 bg-green-50/60 placeholder-green-300 focus:outline-none focus:ring-2 transition ${
      getFieldError(field)
        ? 'border-red-300 focus:border-red-400 focus:ring-red-100'
        : 'border-green-200 focus:border-green-400 focus:ring-green-200'
    }`;

  return (
    <div className="min-h-screen flex items-center justify-center bg-gradient-to-br from-green-50 via-emerald-50 to-teal-50">
      <div className="bg-white/70 backdrop-blur-md border border-green-100 rounded-3xl shadow-lg shadow-green-100 p-10 w-full max-w-sm">
        <div className="text-4xl text-center mb-3">🌿</div>
        <h1 className="text-2xl font-bold text-green-800 text-center mb-1 tracking-tight">Welcome back</h1>
        <p className="text-sm text-green-400 text-center mb-8">Sign in to your account</p>

        <form onSubmit={handleLogin} noValidate>
          <div className="mb-5">
            <label className="block text-xs font-semibold text-green-700 uppercase tracking-wide mb-2">Email</label>
            <input
              type="email"
              value={email}
              onChange={(e) => setEmail(e.target.value)}
              onBlur={(e) => touchField('email', e.target.value)}
              placeholder="you@example.com"
              className={inputClass('email')}
            />
            {getFieldError('email') && <p className="text-red-400 text-xs mt-1">{getFieldError('email')}</p>}
          </div>

          <div className="mb-5">
            <label className="block text-xs font-semibold text-green-700 uppercase tracking-wide mb-2">Password</label>
            <input
              type="password"
              value={password}
              onChange={(e) => setPassword(e.target.value)}
              onBlur={(e) => touchField('password', e.target.value)}
              placeholder="••••••••"
              className={inputClass('password')}
            />
            {getFieldError('password') && <p className="text-red-400 text-xs mt-1">{getFieldError('password')}</p>}
          </div>

          {apiError && (
          <p
            data-testid="api-error"
            className="text-red-500 text-xs bg-red-50 border border-red-100 rounded-lg px-3 py-2 mb-4"
          >
            {apiError}
          </p>
        )}

          <button
            type="submit"
            disabled={loading}
            className="w-full bg-gradient-to-r from-green-500 to-emerald-600 hover:from-green-600 hover:to-emerald-700 text-white font-semibold py-2.5 rounded-xl shadow-md shadow-green-200 transition disabled:opacity-50 disabled:cursor-not-allowed text-sm tracking-wide"
          >
            {loading ? 'Signing in…' : 'Sign in'}
          </button>
        </form>

        <p className="text-center text-xs text-green-400 mt-6">
          Don't have an account?{' '}
          <Link to={ROUTES.REGISTER} className="text-green-600 font-semibold hover:underline">Create one</Link>
        </p>
      </div>
    </div>
  );
}

export default LoginPage;