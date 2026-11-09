import { useState, useEffect, useRef } from 'react';
import { useParams, useNavigate, Link } from 'react-router-dom';
import { getClassById, createHold, getHoldById, confirmHold, cancelHold } from '../services/api';
import { useStudent } from '../components/StudentContext';
import StatusBadge from '../components/StatusBadge';

export default function ApplyPage() {
  const { classId } = useParams();
  const navigate = useNavigate();
  const { studentId, setStudentId, isValidStudentId } = useStudent();

  const [classInfo, setClassInfo] = useState(null);
  const [loadingClass, setLoadingClass] = useState(true);

  // Application / Hold state
  const [hold, setHold] = useState(null);
  const [submittingHold, setSubmittingHold] = useState(false);
  const [confirming, setConfirming] = useState(false);
  const [cancelling, setCancelling] = useState(false);

  // Time remaining (seconds)
  const [secondsRemaining, setSecondsRemaining] = useState(null);

  // Messages
  const [errorMessage, setErrorMessage] = useState('');
  const [successMessage, setSuccessMessage] = useState('');

  const isMountedRef = useRef(true);

  useEffect(() => {
    document.title = 'Apply for Admission | School Admission';
    isMountedRef.current = true;

    async function loadClass() {
      try {
        const data = await getClassById(classId);
        if (isMountedRef.current) {
          setClassInfo(data);
        }
      } catch (err) {
        if (isMountedRef.current) {
          setErrorMessage(err.message || 'Unable to load class information.');
        }
      } finally {
        if (isMountedRef.current) {
          setLoadingClass(false);
        }
      }
    }

    loadClass();

    return () => {
      isMountedRef.current = false;
    };
  }, [classId]);

  // Live countdown timer and expiration synchronization
  useEffect(() => {
    if (!hold || hold.status !== 'HELD' || !hold.expiresAt) {
      setSecondsRemaining(null);
      return;
    }

    const calculateRemaining = () => {
      const expiresAtMs = new Date(hold.expiresAt).getTime();
      const nowMs = Date.now();
      const diffSeconds = Math.max(0, Math.floor((expiresAtMs - nowMs) / 1000));
      return diffSeconds;
    };

    setSecondsRemaining(calculateRemaining());

    const timer = setInterval(async () => {
      const remaining = calculateRemaining();
      setSecondsRemaining(remaining);

      if (remaining <= 0) {
        clearInterval(timer);
        // Sync with backend to get official EXPIRED status
        try {
          const updated = await getHoldById(hold.holdId);
          if (isMountedRef.current && updated) {
            setHold(updated);
          }
        } catch {
          if (isMountedRef.current) {
            setHold((prev) => (prev ? { ...prev, status: 'EXPIRED' } : prev));
          }
        }
      }
    }, 1000);

    return () => clearInterval(timer);
  }, [hold]);

  // Step 1: Submit Seat Reservation (POST /classes/{classId}/holds)
  const handleReserve = async (e) => {
    e.preventDefault();
    setErrorMessage('');
    setSuccessMessage('');

    if (!isValidStudentId) {
      setErrorMessage('Please provide a valid 17-digit birth registration number.');
      return;
    }

    setSubmittingHold(true);

    try {
      const response = await createHold(classId, studentId);
      setHold(response);
      setSuccessMessage('Seat reservation created successfully! Please confirm admission within the reservation window.');

      // Refresh class seat count
      const updatedClass = await getClassById(classId);
      setClassInfo(updatedClass);
    } catch (err) {
      setErrorMessage(err.message || 'Failed to create seat reservation.');
    } finally {
      setSubmittingHold(false);
    }
  };

  // Step 2: Simulated Payment & Confirmation (POST /holds/{holdId}/confirm)
  const handleConfirm = async () => {
    if (!hold) return;
    setErrorMessage('');
    setSuccessMessage('');
    setConfirming(true);

    try {
      const confirmed = await confirmHold(hold.holdId, studentId);
      setHold(confirmed);
      setSuccessMessage('Admission successfully confirmed! Your seat is secured.');

      // Refresh class seat count
      const updatedClass = await getClassById(classId);
      setClassInfo(updatedClass);
    } catch (err) {
      setErrorMessage(err.message || 'Failed to confirm admission.');
      // Refresh hold state in case it expired on backend
      try {
        const fresh = await getHoldById(hold.holdId);
        setHold(fresh);
      } catch {
        // Ignore
      }
    } finally {
      setConfirming(false);
    }
  };

  // Step 3: Cancellation (DELETE /holds/{holdId})
  const handleCancel = async () => {
    if (!hold) return;
    if (!window.confirm('Are you sure you want to cancel this reservation? The seat will be released immediately.')) {
      return;
    }

    setErrorMessage('');
    setSuccessMessage('');
    setCancelling(true);

    try {
      await cancelHold(hold.holdId, studentId);
      setHold((prev) => ({ ...prev, status: 'CANCELLED' }));
      setSuccessMessage('Reservation has been cancelled and the seat has been released.');

      // Refresh class seat count
      const updatedClass = await getClassById(classId);
      setClassInfo(updatedClass);
    } catch (err) {
      setErrorMessage(err.message || 'Failed to cancel reservation.');
    } finally {
      setCancelling(false);
    }
  };

  const formatCountdown = (totalSeconds) => {
    if (totalSeconds == null || totalSeconds <= 0) return '00:00';
    const mins = Math.floor(totalSeconds / 60);
    const secs = totalSeconds % 60;
    return `${String(mins).padStart(2, '0')}:${String(secs).padStart(2, '0')}`;
  };

  if (loadingClass) {
    return (
      <div className="page-container">
        <div className="loading-container">
          <div className="spinner"></div>
          <p>Loading class admission details...</p>
        </div>
      </div>
    );
  }

  if (!classInfo && errorMessage) {
    return (
      <div className="page-container">
        <div className="alert alert-error">
          <strong>Error:</strong> {errorMessage}
        </div>
        <Link to="/classes" className="btn btn-outline">
          &larr; Back to Class List
        </Link>
      </div>
    );
  }

  return (
    <div className="page-container">
      <div className="page-header">
        <div className="breadcrumb">
          <Link to="/classes">&larr; Back to Classes</Link>
        </div>
        <h1>Apply for Admission</h1>
        <p className="page-subtitle">
          Submit your student birth registration number to hold and confirm your seat in {classInfo.name}.
        </p>
      </div>

      {/* Class Overview Card */}
      <div className="summary-card">
        <div className="summary-details">
          <h2>{classInfo.name}</h2>
          <span className="class-id-tag">Class ID: {classInfo.id}</span>
        </div>
        <div className="seat-metrics-grid">
          <div className="metric-box">
            <span className="metric-label">Total Seats</span>
            <span className="metric-value">{classInfo.totalSeats}</span>
          </div>
          <div className="metric-box">
            <span className="metric-label">Held Seats</span>
            <span className="metric-value">{classInfo.heldSeats}</span>
          </div>
          <div className="metric-box">
            <span className="metric-label">Confirmed Seats</span>
            <span className="metric-value">{classInfo.confirmedSeats}</span>
          </div>
          <div className="metric-box highlight">
            <span className="metric-label">Available Seats</span>
            <span className={`metric-value ${classInfo.availableSeats <= 0 ? 'text-danger' : 'text-success'}`}>
              {classInfo.availableSeats}
            </span>
          </div>
        </div>
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

      {/* State A: No Hold Created Yet */}
      {!hold && (
        <div className="form-card">
          <div className="card-header">
            <h3>Step 1: Student Identification & Seat Reservation</h3>
            <p>
              Please enter your official 17-digit Birth Registration Number. Upon submission, a temporary seat reservation will be created.
            </p>
          </div>

          <form onSubmit={handleReserve}>
            <div className="form-group">
              <label htmlFor="studentIdInput" className="form-label">
                17-Digit Birth Registration Number <span className="required">*</span>
              </label>
              <input
                id="studentIdInput"
                type="text"
                inputMode="numeric"
                pattern="[0-9]*"
                maxLength={17}
                placeholder="e.g. 20152691234567890"
                value={studentId}
                onChange={(e) => setStudentId(e.target.value.replace(/\D/g, '').slice(0, 17))}
                className={`form-input ${isValidStudentId ? 'is-valid' : ''}`}
                disabled={submittingHold}
                required
              />
              <small className="form-hint">
                Must be exactly 17 digits. Current length: {studentId.length}/17
              </small>
            </div>

            {classInfo.availableSeats <= 0 && (
              <div className="alert alert-warning">
                <strong>Class Full:</strong> There are currently 0 available seats in this class. You may still attempt a reservation in case an unconfirmed hold expires.
              </div>
            )}

            <div className="form-actions">
              <button
                type="submit"
                className="btn btn-primary"
                disabled={submittingHold || !isValidStudentId}
              >
                {submittingHold ? 'Reserving Seat...' : 'Reserve Seat (Create Hold)'}
              </button>
              <button
                type="button"
                className="btn btn-outline"
                onClick={() => navigate('/classes')}
                disabled={submittingHold}
              >
                Cancel
              </button>
            </div>
          </form>
        </div>
      )}

      {/* State B: Hold Created & Active (or Confirmed / Expired / Cancelled) */}
      {hold && (
        <div className="reservation-card">
          <div className="reservation-header">
            <div>
              <h3>Reservation Details</h3>
              <p className="text-muted">Hold ID: <code>{hold.holdId}</code></p>
            </div>
            <div className="reservation-status-wrapper">
              <StatusBadge status={hold.status} />
            </div>
          </div>

          <div className="reservation-grid">
            <div className="info-row">
              <span className="info-label">Class:</span>
              <span className="info-value">{classInfo.name}</span>
            </div>
            <div className="info-row">
              <span className="info-label">Student ID:</span>
              <span className="info-value font-mono">{hold.studentId}</span>
            </div>
            <div className="info-row">
              <span className="info-label">Status:</span>
              <span className="info-value"><StatusBadge status={hold.status} /></span>
            </div>
            <div className="info-row">
              <span className="info-label">Expires At:</span>
              <span className="info-value font-mono">
                {hold.expiresAt ? new Date(hold.expiresAt).toLocaleString() : 'N/A'}
              </span>
            </div>
          </div>

          {/* Active Hold Countdown & Payment */}
          {hold.status === 'HELD' && (
            <div className="action-section">
              <div className="timer-banner">
                <span className="timer-label">Time Remaining to Complete Admission:</span>
                <span className={`timer-digits ${secondsRemaining !== null && secondsRemaining < 60 ? 'timer-danger' : ''}`}>
                  {formatCountdown(secondsRemaining)}
                </span>
                <p className="timer-explanation">
                  Your seat is reserved temporarily. If not confirmed before expiration, the reservation will automatically become EXPIRED and the seat will be returned to the pool.
                </p>
              </div>

              <div className="action-buttons">
                <button
                  type="button"
                  className="btn btn-success btn-lg"
                  onClick={handleConfirm}
                  disabled={confirming || cancelling || (secondsRemaining !== null && secondsRemaining <= 0)}
                  title="Simulate payment and complete admission confirmation"
                >
                  {confirming ? 'Processing Confirmation...' : 'Simulate Payment & Confirm Admission'}
                </button>

                <button
                  type="button"
                  className="btn btn-danger"
                  onClick={handleCancel}
                  disabled={confirming || cancelling}
                >
                  {cancelling ? 'Cancelling...' : 'Cancel Reservation'}
                </button>
              </div>
            </div>
          )}

          {/* Expired State */}
          {hold.status === 'EXPIRED' && (
            <div className="alert alert-warning mt-4">
              <h4>Reservation Expired</h4>
              <p>
                Your reservation window has ended and this hold has expired. The seat has been released automatically and returned to the pool of available seats.
              </p>
              <div className="alert-actions mt-2">
                <button
                  type="button"
                  className="btn btn-primary btn-sm"
                  onClick={() => setHold(null)}
                >
                  Try Reserving Again
                </button>
                <Link to="/classes" className="btn btn-outline btn-sm">
                  View Other Classes
                </Link>
              </div>
            </div>
          )}

          {/* Confirmed State */}
          {hold.status === 'CONFIRMED' && (
            <div className="alert alert-success mt-4">
              <h4>Admission Confirmed</h4>
              <p>
                Congratulations! Your admission to <strong>{classInfo.name}</strong> has been officially confirmed and your seat is secured.
              </p>
              <div className="alert-actions mt-2">
                <Link to="/my-applications" className="btn btn-primary btn-sm">
                  View in My Applications
                </Link>
                <Link to="/classes" className="btn btn-outline btn-sm">
                  Browse Classes
                </Link>
              </div>
            </div>
          )}

          {/* Cancelled State */}
          {hold.status === 'CANCELLED' && (
            <div className="alert alert-neutral mt-4">
              <h4>Reservation Cancelled</h4>
              <p>
                This reservation was cancelled. The reserved seat has been released and is available for other students.
              </p>
              <div className="alert-actions mt-2">
                <button
                  type="button"
                  className="btn btn-primary btn-sm"
                  onClick={() => setHold(null)}
                >
                  Apply Again
                </button>
                <Link to="/classes" className="btn btn-outline btn-sm">
                  Browse Classes
                </Link>
              </div>
            </div>
          )}
        </div>
      )}
    </div>
  );
}
