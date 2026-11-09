import { Routes, Route, Navigate } from 'react-router-dom';
import { StudentProvider } from './components/StudentContext';
import Navbar from './components/Navbar';
import ClassListPage from './pages/ClassListPage';
import CreateClassPage from './pages/CreateClassPage';
import ApplyPage from './pages/ApplyPage';
import MyApplicationsPage from './pages/MyApplicationsPage';

export default function App() {
  return (
    <StudentProvider>
      <div className="app-shell">
        <Navbar />

        <main className="main-content">
          <Routes>
            <Route path="/" element={<Navigate to="/classes" replace />} />
            <Route path="/classes" element={<ClassListPage />} />
            <Route path="/admin/classes/new" element={<CreateClassPage />} />
            <Route path="/classes/:classId/apply" element={<ApplyPage />} />
            <Route path="/my-applications" element={<MyApplicationsPage />} />
            <Route path="*" element={<Navigate to="/classes" replace />} />
          </Routes>
        </main>

        <footer className="footer">
          <div className="footer-container">
            <p className="footer-text">
              School Admission System &bull; Assistant Software Engineer Recruitment Project
            </p>
          </div>
        </footer>
      </div>
    </StudentProvider>
  );
}
