import { useState, useEffect, useRef } from 'react';
import { Link } from 'react-router-dom';
import { getClasses } from '../services/api';

export default function ClassListPage() {
  const [classes, setClasses] = useState([]);
  const [loading, setLoading] = useState(true);
  const [refreshing, setRefreshing] = useState(false);
  const [errorMessage, setErrorMessage] = useState('');
  const [lastUpdated, setLastUpdated] = useState(null);

  const isMountedRef = useRef(true);

  const fetchClasses = async (isManual = false) => {
    if (isManual) {
      setRefreshing(true);
    }
    setErrorMessage('');

    try {
      const data = await getClasses();
      if (isMountedRef.current) {
        setClasses(Array.isArray(data) ? data : []);
        setLastUpdated(new Date());
      }
    } catch (err) {
      if (isMountedRef.current) {
        setErrorMessage(err.message || 'Unable to load classes from the backend.');
      }
    } finally {
      if (isMountedRef.current) {
        setLoading(false);
        setRefreshing(false);
      }
    }
  };

  useEffect(() => {
    document.title = 'Classes | School Admission';
    isMountedRef.current = true;

    fetchClasses();

    // Auto-refresh seat statistics every 5 seconds while page is open (Requirement 9)
    const interval = setInterval(() => {
      fetchClasses();
    }, 5000);

    return () => {
      isMountedRef.current = false;
      clearInterval(interval);
    };
  }, []);

  return (
    <div className="page-container">
      <div className="page-header flex-header">
        <div>
          <h1>Available Classes</h1>
          <p className="page-subtitle">
            View real-time seat availability across all shifts and apply for admission.
          </p>
        </div>
        <div className="header-actions">
          <button
            type="button"
            className="btn btn-outline btn-sm"
            onClick={() => fetchClasses(true)}
            disabled={refreshing}
            title="Refresh current seat information"
          >
            {refreshing ? 'Updating...' : 'Refresh Seats'}
          </button>
          <Link to="/admin/classes/new" className="btn btn-primary btn-sm">
            + Create Class
          </Link>
        </div>
      </div>

      {lastUpdated && (
        <div className="status-bar">
          <span className="live-indicator">
            <span className="live-dot"></span> Live seat count auto-refreshes every 5s
          </span>
          <span className="last-sync">
            Last updated: {lastUpdated.toLocaleTimeString()}
          </span>
        </div>
      )}

      {errorMessage && (
        <div className="alert alert-error" role="alert">
          <strong>Error:</strong> {errorMessage}
          <button
            type="button"
            className="btn btn-xs btn-outline ml-2"
            onClick={() => fetchClasses(true)}
          >
            Retry
          </button>
        </div>
      )}

      {loading ? (
        <div className="loading-container">
          <div className="spinner"></div>
          <p>Loading classes and live seat information...</p>
        </div>
      ) : classes.length === 0 ? (
        <div className="empty-state">
          <p className="empty-title">No classes available yet</p>
          <p className="empty-description">
            There are currently no admission classes registered in the system.
          </p>
          <Link to="/admin/classes/new" className="btn btn-primary">
            Create First Class
          </Link>
        </div>
      ) : (
        <div className="table-responsive">
          <table className="data-table">
            <thead>
              <tr>
                <th>Class & Shift Name</th>
                <th className="text-center">Total Seats</th>
                <th className="text-center">Held Seats</th>
                <th className="text-center">Confirmed Seats</th>
                <th className="text-center">Available Seats</th>
                <th className="text-right">Action</th>
              </tr>
            </thead>
            <tbody>
              {classes.map((cls) => {
                const isFull = cls.availableSeats <= 0;
                return (
                  <tr key={cls.id} className={isFull ? 'row-full' : ''}>
                    <td className="font-semibold">
                      {cls.name}
                      <span className="class-id-tag">ID: {cls.id}</span>
                    </td>
                    <td className="text-center">{cls.totalSeats}</td>
                    <td className="text-center">
                      <span className="badge badge-held">{cls.heldSeats}</span>
                    </td>
                    <td className="text-center">
                      <span className="badge badge-confirmed">{cls.confirmedSeats}</span>
                    </td>
                    <td className="text-center font-bold">
                      <span className={`seat-count ${isFull ? 'seat-zero' : 'seat-positive'}`}>
                        {cls.availableSeats}
                      </span>
                    </td>
                    <td className="text-right">
                      {isFull ? (
                        <span className="badge badge-full">Seats Full</span>
                      ) : (
                        <Link
                          to={`/classes/${cls.id}/apply`}
                          className="btn btn-primary btn-sm"
                        >
                          Apply Now
                        </Link>
                      )}
                    </td>
                  </tr>
                );
              })}
            </tbody>
          </table>
        </div>
      )}
    </div>
  );
}
