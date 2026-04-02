import { useState } from 'react';
import { useNavigate, Link } from 'react-router-dom';
import api from '../services/api';
import { useFormValidation } from '../hooks/useFormValidation';
import { ROUTES } from '../constants/routes';

function RegisterPage() {
  const [formData, setFormData] = useState({
    name: '',
    email: '',
    password: '',
    confirmPassword: '',
  });
  const [apiError, setApiError] = useState('');
  const [loading, setLoading] = useState(false);
  const navigate = useNavigate();

  const { validateAll, touchField, getFieldError } = useFormValidation([
    'name',
    'email',
    'password',
    'confirmPassword',
  ]);

  const handleChange = (e) => {
    setFormData({ ...formData, [e.target.name]: e.target.value });
  };

  const handleRegister = async (e) => {
    e.preventDefault();
    setApiError('');
    if (!validateAll(formData)) return;
    setLoading(true);
    try {
      await api.post('/register', {
        email: formData.email,
        password: formData.password,
      });
      navigate(ROUTES.LOGIN);
    } catch (error) {
      setApiError(error.response?.data?.detail || error.response?.data?.title || 'Registration failed');
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

        <div className="text-4xl text-center mb-3">🌱</div>
        <h1 className="text-2xl font-bold text-green-800 text-center mb-1 tracking-tight">Create account</h1>
        <p className="text-sm text-green-400 text-center mb-8">Begin your journey</p>

        <form onSubmit={handleRegister} noValidate>

          {/* Name */}
          <div className="mb-4">
            <label className="block text-xs font-semibold text-green-700 uppercase tracking-wide mb-2">
              Name
            </label>
            <input
              type="text"
              name="name"
              value={formData.name}
              onChange={handleChange}
              onBlur={(e) => touchField('name', e.target.value, formData)}
              placeholder="Your name"
              className={inputClass('name')}
            />
            {getFieldError('name') && (
              <p className="text-red-400 text-xs mt-1">{getFieldError('name')}</p>
            )}
          </div>

          {/* Email */}
          <div className="mb-4">
            <label className="block text-xs font-semibold text-green-700 uppercase tracking-wide mb-2">
              Email
            </label>
            <input
              type="email"
              name="email"
              value={formData.email}
              onChange={handleChange}
              onBlur={(e) => touchField('email', e.target.value, formData)}
              placeholder="you@example.com"
              className={inputClass('email')}
            />
            {getFieldError('email') && (
              <p className="text-red-400 text-xs mt-1">{getFieldError('email')}</p>
            )}
          </div>

          {/* Password */}
          <div className="mb-4">
            <label className="block text-xs font-semibold text-green-700 uppercase tracking-wide mb-2">
              Password
            </label>
            <input
              type="password"
              name="password"
              value={formData.password}
              onChange={handleChange}
              onBlur={(e) => touchField('password', e.target.value, formData)}
              placeholder="••••••••"
              className={inputClass('password')}
            />
            {getFieldError('password') && (
              <p className="text-red-400 text-xs mt-1">{getFieldError('password')}</p>
            )}
          </div>

          {/* Confirm Password */}
          <div className="mb-6">
            <label className="block text-xs font-semibold text-green-700 uppercase tracking-wide mb-2">
              Confirm Password
            </label>
            <input
              type="password"
              name="confirmPassword"
              value={formData.confirmPassword}
              onChange={handleChange}
              onBlur={(e) => touchField('confirmPassword', e.target.value, formData)}
              placeholder="••••••••"
              className={inputClass('confirmPassword')}
            />
            {getFieldError('confirmPassword') && (
              <p className="text-red-400 text-xs mt-1">{getFieldError('confirmPassword')}</p>
            )}
          </div>

          {/* API Error */}
          {apiError && (
            <p  data-testid="api-error" className="text-red-500 text-xs bg-red-50 border border-red-100 rounded-lg px-3 py-2 mb-4">
              {apiError}
            </p>
          )}

          <button
            type="submit"
            disabled={loading}
            className="w-full bg-gradient-to-r from-green-500 to-emerald-600 hover:from-green-600 hover:to-emerald-700 text-white font-semibold py-2.5 rounded-xl shadow-md shadow-green-200 transition disabled:opacity-50 disabled:cursor-not-allowed text-sm tracking-wide"
          >
            {loading ? 'Creating account…' : 'Create account'}
          </button>

        </form>

        <p className="text-center text-xs text-green-400 mt-6">
          Already have an account?{' '}
          <Link to={ROUTES.LOGIN} className="text-green-600 font-semibold hover:underline">Sign in</Link>
        </p>

      </div>
    </div>
  );
}

export default RegisterPage;