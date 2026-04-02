import { useNavigate } from 'react-router-dom';

function DashboardPage() {
  const navigate = useNavigate();

  const handleLogout = () => {
    localStorage.removeItem('token');
    navigate('/');
  };

  return (
    <div className="min-h-screen flex items-center justify-center bg-gradient-to-br from-green-50 via-emerald-50 to-teal-50">
      <div className="bg-white/70 backdrop-blur-md border border-green-100 rounded-3xl shadow-lg shadow-green-100 p-10 w-full max-w-sm text-center">
        <div className="text-4xl mb-3">🌿</div>
        <h1 className="text-2xl font-bold text-green-800 mb-1">Dashboard</h1>
        <p className="text-sm text-green-400 mb-8">You're logged in!</p>
        <button
          onClick={handleLogout}
          className="w-full bg-gradient-to-r from-green-500 to-emerald-600 hover:from-green-600 hover:to-emerald-700 text-white font-semibold py-2.5 rounded-xl shadow-md shadow-green-200 transition text-sm tracking-wide"
        >
          Log out
        </button>
      </div>
    </div>
  );
}

export default DashboardPage;