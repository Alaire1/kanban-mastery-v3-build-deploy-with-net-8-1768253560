import { useState } from 'react';

const validators = {
  name: (value) => {
    if (!value.trim()) return 'Name is required';
    if (value.trim().length < 2) return 'Name must be at least 2 characters';
    return null;
  },
  email: (value) => {
    if (!value.trim()) return 'Email is required';
    if (!/^[^\s@]+@[^\s@]+\.[^\s@]+$/.test(value)) return 'Enter a valid email address';
    return null;
  },
  password: (value) => {
    if (!value) return 'Password is required';
    if (value.length < 8) return 'Password must be at least 8 characters';
    if (!/[A-Z]/.test(value)) return 'Password must contain at least one uppercase letter';
    if (!/[0-9]/.test(value)) return 'Password must contain at least one number';
    return null;
  },
  confirmPassword: (value, allValues) => {
    if (!value) return 'Please confirm your password';
    if (value !== allValues.password) return 'Passwords do not match';
    return null;
  },
};

export function useFormValidation(fields) {
  const [errors, setErrors] = useState({});
  const [touched, setTouched] = useState({});

  const validateField = (name, value, allValues = {}) => {
    const validator = validators[name];
    return validator ? validator(value, allValues) : null;
  };

  const touchField = (name, value, allValues = {}) => {
    setTouched((prev) => ({ ...prev, [name]: true }));
    const error = validateField(name, value, allValues);
    setErrors((prev) => ({ ...prev, [name]: error }));
  };

  const validateAll = (values) => {
    const newErrors = {};
    fields.forEach((name) => {
      const error = validateField(name, values[name], values);
      if (error) newErrors[name] = error;
    });
    setErrors(newErrors);
    setTouched(fields.reduce((acc, f) => ({ ...acc, [f]: true }), {}));
    return Object.keys(newErrors).length === 0;
  };

  const getFieldError = (name) => (touched[name] ? errors[name] : null);

  return { validateAll, touchField, getFieldError };
}