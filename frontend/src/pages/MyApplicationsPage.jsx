import { useState, useEffect, useRef } from 'react';
import { Link } from 'react-router-dom';
import { getMyHolds, getClasses, confirmHold, cancelHold } from '../services/api';
import { useStudent } from '../components/StudentContext';
import StatusBadge from '../components/StatusBadge';

export default function MyApplicationsPage() {
  const { studentId, setStudentId, isValidStudentId } = useStudent();

  const [inputStudentId, setInputStudentId] = useState(studentId);
  const [applications, setApplications] = useState([]);
  const [classesMap, setClassesMap] = useState({});
  const [loading, setLoading] = useState(false);
  const [actionLoadingId, setActionLoadingId] = useState(null);
  const [errorMessage, setErrorMessage] = useState('');
  const [successMessage, setSuccessMessage] = useState('');
  const [hasSearched, setHasSearched] = useState(false);

  const isMountedRef = useRef(true);

  // Sync input when studentId context changes (e.g. from top navbar)
  useEffect(() => {
    setInputStudentId(studentId);
  }, [studentId]);

  useEffect(() => {
    document.title = 'My Applications | School Admission';
    isMountedRef.current = true;

    // Load classes map for resolving classId to className
    loadClassesMap();

    // Auto-search if a valid studentId already exists in localStorage/context
    if (isValidStudentId) {
      fetchApplications(studentId);
    }

    return () => {
      isMountedRef.current = false;
    };
  }, []);

  const loadClassesMap = async () => {
    try {
      const classList = await getClasses();
      if (Array.isArray(classList)) {
        const map = {};
        classList.forEach((c) => {
          map[c.id] = c;
        });
        if (isMountedRef.current) {
          setClassesMap(map);
        }
      }
    } catch {
      // Non-critical, fallback to classId
    }
  };

  const fetchApplications = async (idToQuery) => {
    const targetId = (idToQuery || inputStudentId || '').trim();

    if (!targetId || !/^\d{17}$/.test(targetId)) {
      setErrorMessage('Please provide a valid 17-digit birth registration number.');
      return;
    }

    setLoading(true);
    setErrorMessage('');
    setSuccessMessage('');
    setHasSearched(true);

    try {
      const data = await getMyHolds(targetId);
      if (isMountedRef.current) {
        setApplications(Array.isArray(data) ? data : []);
      }
    } catch (err) {
      if (isMountedRef.current) {
        setErrorMessage(err.message || 'Unable to retrieve your applications.');
      }
    } finally {
      if (isMountedRef.current) {
        setLoading(false);
      }
    }
  };

  const handleSearchSubmit = (e) => {
    e.preventDefault();
    const cleaned = inputStudentId.trim();
    setStudentId(cleaned);
    fetchApplications(cleaned);
  };

  const handleConfirm = async (holdId) => {
    setErrorMessage('');
    setSuccessMessage('');
    setActionLoadingId(holdId);

    try {
      await confirmHold(holdId, studentId);
      setSuccessMessage('Admission successfully confirmed! Your seat is secured.');
      await fetchApplications(studentId);
    } catch (err) {
      setErrorMessage(err.message || 'Failed to confirm admission.');
      await fetchApplications(studentId);
    } finally {
      setActionLoadingId(null);
    }
  };

  const handleCancel = async (holdId) => {
    if (!window.confirm('Are you sure you want to cancel this reservation? The seat will be released immediately.')) {
      return;
    }

    setErrorMessage('');
    setSuccessMessage('');
    setActionLoadingId(holdId);

    try {
      await cancelHold(holdId, studentId);
      setSuccessMessage('Reservation cancelled successfully. Seat has been released.');
      await fetchApplications(studentId);
    } catch (err) {
      setErrorMessage(err.message || 'Failed to cancel reservation.');
      await fetchApplications(studentId);
    } finally {
      setActionLoadingId(null);
    }
  };

  return (
    <div className="page-container">
      <div className="page-header">
        <h1>My Applications</h1>
        <p className="page-subtitle">
          Track and manage your admission applications and reservations using your Birth Registration Number.
        </p>
      </div>

      {/* Student ID Lookup Bar */}
      <div className="search-card">
        <form onSubmit={handleSearchSubmit} className="search-form">
          <div className="search-input-group">
            <label htmlFor="searchStudentId" className="form-label">
              Student Birth Registration Number (17 Digits):
            </label>
            <div className="flex-row gap-2">
              <input
                id="searchStudentId"
                type="text"
                inputMode="numeric"
                pattern="[0-9]*"
                maxLength={17}
                placeholder="Enter 17-digit number (e.g. 20152691234567890)"
                value={inputStudentId}
                onChange={(e) => setInputStudentId(e.target.value.replace(/\D/g, '').slice(0, 17))}
                className="form-input"
                disabled={loading}
              />
              <button
                type="submit"
                className="btn btn-primary"
                disabled={loading || inputStudentId.length !== 17}
              >
                {loading ? 'Searching...' : 'Search Applications'}
              </button>
            </div>
            <small className="form-hint">
              Preserved across browser refreshes and sessions. No password required.
            </small>
          </div>
        </form>
      </div>

      {errorMessage && (
        <div className="alert alert-error" role="alert">
          <strong>Error:</strong> {errorMessage}
        </div>
      )}

      {successMessage && (
        <div className="alert alert-success" role="alert">
          <strong>Notice:</strong> {successMessage}
        </div>
      )}

      {/* Applications List */}
      {loading ? (
        <div className="loading-container">
          <div className="spinner"></div>
          <p>Retrieving student applications...</p>
        </div>
      ) : hasSearched && applications.length === 0 ? (
        <div className="empty-state">
          <p className="empty-title">No applications found</p>
          <p className="empty-description">
            No seat reservations or confirmed admissions found for student ID <code>{inputStudentId}</code>.
          </p>
          <Link to="/classes" className="btn btn-primary">
            Explore Available Classes
          </Link>
        </div>
      ) : applications.length > 0 ? (
        <div className="table-responsive">
          <table className="data-table">
            <thead>
              <tr>
                <th>Class & Shift</th>
                <th>Hold ID</th>
                <th>Status</th>
                <th>Created At</th>
                <th>Expiration / Deadline</th>
                <th className="text-right">Actions</th>
              </tr>
            </thead>
            <tbody>
              {applications.map((app) => {
                const classData = classesMap[app.classId];
                const className = classData ? classData.name : app.classId;
                const isHeld = app.status === 'HELD';
                const isExpired = app.status === 'EXPIRED' || (isHeld && new Date(app.expiresAt) <= new Date());
                const isConfirmed = app.status === 'CONFIRMED';
                const isCancelled = app.status === 'CANCELLED';

                const isActionBusy = actionLoadingId === app.holdId;

                return (
                  <tr key={app.holdId}>
                    <td className="font-semibold">
                      {className}
                      <span className="class-id-tag">Class ID: {app.classId}</span>
                    </td>
                    <td>
                      <code>{app.holdId}</code>
                    </td>
                    <td>
                      <StatusBadge status={isExpired && isHeld ? 'EXPIRED' : app.status} />
                    </td>
                    <td className="text-muted text-sm">
                      {app.createdAt ? new Date(app.createdAt).toLocaleString() : 'N/A'}
                    </td>
                    <td className="text-sm">
                      {app.expiresAt ? (
                        <span className={isExpired ? 'text-muted' : 'text-danger font-semibold'}>
                          {new Date(app.expiresAt).toLocaleTimeString()}
                        </span>
                      ) : (
                        'N/A'
                      )}
                    </td>
                    <td className="text-right">
                      {isHeld && !isExpired && (
                        <div className="action-button-group">
                          <button
                            type="button"
                            className="btn btn-success btn-xs"
                            onClick={() => handleConfirm(app.holdId)}
                            disabled={isActionBusy}
                            title="Simulate payment and confirm admission"
                          >
                            {isActionBusy ? 'Processing...' : 'Pay & Confirm'}
                          </button>
                          <button
                            type="button"
                            className="btn btn-danger btn-xs"
                            onClick={() => handleCancel(app.holdId)}
                            disabled={isActionBusy}
                            title="Cancel this reservation"
                          >
                            {isActionBusy ? '...' : 'Cancel'}
                          </button>
                        </div>
                      )}

                      {isConfirmed && (
                        <span className="badge badge-confirmed">Confirmed</span>
                      )}

                      {(isExpired || isCancelled) && (
                        <Link
                          to={`/classes/${app.classId}/apply`}
                          className="btn btn-outline btn-xs"
                        >
                          Re-Apply
                        </Link>
                      )}
                    </td>
                  </tr>
                );
              })}
            </tbody>
          </table>
        </div>
      ) : null}
    </div>
  );
}
