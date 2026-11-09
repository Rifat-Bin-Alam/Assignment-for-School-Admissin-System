import { createContext, useContext, useState, useEffect } from 'react';

const STORAGE_KEY = 'school_admission_student_id';

const StudentContext = createContext({
  studentId: '',
  setStudentId: () => {},
  isValidStudentId: false,
  formatStudentId: () => ''
});

export function StudentProvider({ children }) {
  const [studentId, setStudentIdState] = useState(() => {
    return localStorage.getItem(STORAGE_KEY) || '';
  });

  const isValidStudentId = /^\d{17}$/.test(studentId.trim());

  const setStudentId = (id) => {
    const cleaned = (id || '').trim();
    setStudentIdState(cleaned);
    if (cleaned) {
      localStorage.setItem(STORAGE_KEY, cleaned);
    } else {
      localStorage.removeItem(STORAGE_KEY);
    }
  };

  return (
    <StudentContext.Provider
      value={{
        studentId,
        setStudentId,
        isValidStudentId
      }}
    >
      {children}
    </StudentContext.Provider>
  );
}

export function useStudent() {
  return useContext(StudentContext);
}
