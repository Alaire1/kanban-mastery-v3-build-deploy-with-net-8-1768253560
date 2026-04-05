import { Navigate } from 'react-router-dom';
import { ROUTES } from '../constants/routes';

function ProtectedRoute({ children }) {
  const token = localStorage.getItem('token');
  return token ? children : <Navigate to={ROUTES.LOGIN} replace />;
}

export default ProtectedRoute;