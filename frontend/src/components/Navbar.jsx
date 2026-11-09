import { useState, useEffect } from 'react';
import { NavLink, Link } from 'react-router-dom';
import { useStudent } from './StudentContext';

export default function Navbar() {
  const { studentId, setStudentId, isValidStudentId } = useStudent();
  const [localInput, setLocalInput] = useState(studentId);

  useEffect(() => {
    setLocalInput(studentId);
  }, [studentId]);

  const handleChange = (e) => {
    // Only accept numeric digits, up to 17 digits
    const value = e.target.value.replace(/\D/g, '').slice(0, 17);
    setLocalInput(value);
    setStudentId(value);
  };

  return (
    <header className="navbar">
      <div className="navbar-container">
        <div className="navbar-brand">
          <Link to="/classes" className="brand-link">
            School Admission System
          </Link>
        </div>

        <nav className="navbar-links">
          <NavLink
            to="/classes"
            className={({ isActive }) => (isActive ? 'nav-link active' : 'nav-link')}
          >
            Available Classes
          </NavLink>
          <NavLink
            to="/my-applications"
            className={({ isActive }) => (isActive ? 'nav-link active' : 'nav-link')}
          >
            My Applications
          </NavLink>
          <NavLink
            to="/admin/classes/new"
            className={({ isActive }) => (isActive ? 'nav-link active' : 'nav-link')}
          >
            Create Class
          </NavLink>
        </nav>

        <div className="navbar-student-id">
          <label htmlFor="nav-student-id" className="student-id-label">
            Birth Reg. No:
          </label>
          <div className="student-id-input-wrapper">
            <input
              id="nav-student-id"
              type="text"
              inputMode="numeric"
              pattern="[0-9]*"
              maxLength={17}
              placeholder="17-digit number"
              value={localInput}
              onChange={handleChange}
              className={`student-id-input ${isValidStudentId ? 'is-valid' : localInput.length > 0 ? 'is-incomplete' : ''}`}
              title="Enter your 17-digit birth registration number"
            />
            {localInput.length > 0 && (
              <span className={`student-id-badge ${isValidStudentId ? 'badge-valid' : 'badge-count'}`}>
                {isValidStudentId ? 'Verified' : `${localInput.length}/17`}
              </span>
            )}
          </div>
        </div>
      </div>
    </header>
  );
}
