import { useState, useEffect } from 'react';
import { useNavigate, Link } from 'react-router-dom';
import { createClass } from '../services/api';

export default function CreateClassPage() {
  const navigate = useNavigate();

  const [name, setName] = useState('');
  const [totalSeats, setTotalSeats] = useState('');
  const [loading, setLoading] = useState(false);
  const [successMessage, setSuccessMessage] = useState('');
  const [errorMessage, setErrorMessage] = useState('');
  const [createdClass, setCreatedClass] = useState(null);

  useEffect(() => {
    document.title = 'Create Class | School Admission';
  }, []);

  const handleSubmit = async (e) => {
    e.preventDefault();
    setSuccessMessage('');
    setErrorMessage('');
    setCreatedClass(null);

    const trimmedName = name.trim();
    const seatsNumber = parseInt(totalSeats, 10);

    if (!trimmedName) {
      setErrorMessage('Please enter a class name.');
      return;
    }

    if (isNaN(seatsNumber) || seatsNumber <= 0) {
      setErrorMessage('Total seats must be a positive number greater than 0.');
      return;
    }

    setLoading(true);

    try {
      const response = await createClass({
        name: trimmedName,
        totalSeats: seatsNumber
      });

      setCreatedClass(response);
      setSuccessMessage(`Class "${response.name}" successfully created with ${response.totalSeats} seats.`);
      setName('');
      setTotalSeats('');
    } catch (err) {
      setErrorMessage(err.message || 'Failed to create class. Please try again.');
    } finally {
      setLoading(false);
    }
  };

  return (
    <div className="page-container">
      <div className="page-header">
        <h1>Create Class</h1>
        <p className="page-subtitle">
          Define a new class and shift combination with its dedicated seat capacity.
        </p>
      </div>

      <div className="form-card">
        {successMessage && (
          <div className="alert alert-success" role="alert">
            <div className="alert-content">
              <strong>Success:</strong> {successMessage}
            </div>
            {createdClass && (
              <div className="alert-actions">
                <Link to="/classes" className="btn btn-sm btn-secondary">
                  View in Class List
                </Link>
                <Link to={`/classes/${createdClass.id}/apply`} className="btn btn-sm btn-primary">
                  Apply Now
                </Link>
              </div>
            )}
          </div>
        )}

        {errorMessage && (
          <div className="alert alert-error" role="alert">
            <strong>Error:</strong> {errorMessage}
          </div>
        )}

        <form onSubmit={handleSubmit} noValidate>
          <div className="form-group">
            <label htmlFor="className" className="form-label">
              Class Name & Shift <span className="required">*</span>
            </label>
            <input
              id="className"
              type="text"
              className="form-input"
              placeholder="e.g. Class 6 - Morning Shift"
              value={name}
              onChange={(e) => setName(e.target.value)}
              disabled={loading}
              required
            />
            <small className="form-hint">
              Remember that different shifts (e.g. Morning Shift vs. Day Shift) must be created as separate class records.
            </small>
          </div>

          <div className="form-group">
            <label htmlFor="totalSeats" className="form-label">
              Total Seat Capacity <span className="required">*</span>
            </label>
            <input
              id="totalSeats"
              type="number"
              min="1"
              step="1"
              className="form-input"
              placeholder="e.g. 120"
              value={totalSeats}
              onChange={(e) => setTotalSeats(e.target.value)}
              disabled={loading}
              required
            />
          </div>

          <div className="form-actions">
            <button
              type="submit"
              className="btn btn-primary"
              disabled={loading}
            >
              {loading ? 'Creating Class...' : 'Create Class'}
            </button>
            <button
              type="button"
              className="btn btn-outline"
              onClick={() => navigate('/classes')}
              disabled={loading}
            >
              Cancel
            </button>
          </div>
        </form>
      </div>
    </div>
  );
}
