// Base API URL configured via environment variable
const BASE_URL = (import.meta.env.VITE_API_BASE_URL || 'http://localhost:5119').replace(/\/+$/, '');

/**
 * Custom Error class that holds HTTP status code and backend error message.
 */
export class ApiError extends Error {
  constructor(message, status, data = null) {
    super(message);
    this.name = 'ApiError';
    this.status = status;
    this.data = data;
  }
}

/**
 * Universal fetch wrapper for API requests.
 */
async function request(endpoint, options = {}) {
  const url = `${BASE_URL}${endpoint}`;
  const headers = {
    'Accept': 'application/json',
    ...(options.headers || {})
  };

  if (options.body && typeof options.body === 'object' && !(options.body instanceof FormData)) {
    headers['Content-Type'] = 'application/json';
    options.body = JSON.stringify(options.body);
  }

  let response;
  try {
    response = await fetch(url, { ...options, headers });
  } catch (err) {
    throw new ApiError(
      `Unable to connect to the backend server (${BASE_URL}). Please verify that the backend is running.`,
      0
    );
  }

  // Handle 204 No Content
  if (response.status === 204) {
    return null;
  }

  let data = null;
  const contentType = response.headers.get('content-type');
  if (contentType && contentType.includes('application/json')) {
    try {
      data = await response.json();
    } catch {
      data = null;
    }
  } else {
    try {
      data = await response.text();
    } catch {
      data = null;
    }
  }

  if (!response.ok) {
    const errorMessage =
      (data && typeof data === 'object' && data.message) ||
      (typeof data === 'string' && data) ||
      `Request failed with status code ${response.status}`;

    throw new ApiError(errorMessage, response.status, data);
  }

  return data;
}

// ---------------------------------------------------------------------------
// 1. Classes Endpoints
// ---------------------------------------------------------------------------

/**
 * 1. POST /classes - Create a new class
 * @param {Object} payload { name: string, totalSeats: number }
 */
export async function createClass({ name, totalSeats }) {
  return request('/classes', {
    method: 'POST',
    body: { name, totalSeats: parseInt(totalSeats, 10) }
  });
}

/**
 * 2. GET /classes - Return all classes with seat counts
 */
export async function getClasses() {
  return request('/classes', {
    method: 'GET'
  });
}

/**
 * 3. GET /classes/{classId} - Return one class with seat information
 * @param {string} classId
 */
export async function getClassById(classId) {
  return request(`/classes/${encodeURIComponent(classId)}`, {
    method: 'GET'
  });
}

// ---------------------------------------------------------------------------
// 2. Holds & Reservation Endpoints
// ---------------------------------------------------------------------------

/**
 * 4. POST /classes/{classId}/holds - Create temporary seat reservation
 * @param {string} classId
 * @param {string} studentId
 */
export async function createHold(classId, studentId) {
  return request(`/classes/${encodeURIComponent(classId)}/holds`, {
    method: 'POST',
    headers: {
      'X-Student-Id': studentId
    }
  });
}

/**
 * 5. GET /holds/{holdId} - Return reservation information
 * @param {string} holdId
 */
export async function getHoldById(holdId) {
  return request(`/holds/${encodeURIComponent(holdId)}`, {
    method: 'GET'
  });
}

/**
 * 6. POST /holds/{holdId}/confirm - Confirm an admission
 * @param {string} holdId
 * @param {string} studentId
 */
export async function confirmHold(holdId, studentId) {
  return request(`/holds/${encodeURIComponent(holdId)}/confirm`, {
    method: 'POST',
    headers: {
      'X-Student-Id': studentId
    }
  });
}

/**
 * 7. DELETE /holds/{holdId} - Cancel a reservation
 * @param {string} holdId
 * @param {string} studentId
 */
export async function cancelHold(holdId, studentId) {
  return request(`/holds/${encodeURIComponent(holdId)}`, {
    method: 'DELETE',
    headers: {
      'X-Student-Id': studentId
    }
  });
}

// ---------------------------------------------------------------------------
// 3. Student Applications Endpoints
// ---------------------------------------------------------------------------

/**
 * 8. GET /me/holds - Return all applications belonging to current student
 * @param {string} studentId
 */
export async function getMyHolds(studentId) {
  return request('/me/holds', {
    method: 'GET',
    headers: {
      'X-Student-Id': studentId
    }
  });
}
