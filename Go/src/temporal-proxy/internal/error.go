//-----------------------------------------------------------------------------
// FILE:		errors.go
// CONTRIBUTOR: John C Burns
// COPYRIGHT:	Copyright (c) 2016-2019 by neonFORGE, LLC.  All rights reserved.
//
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
//
//     http://www.apache.org/licenses/LICENSE-2.0
//
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.

package internal

import (
	"errors"
	"fmt"
	"reflect"

	"go.temporal.io/sdk/temporal"
)

const (

	// ApplicationError returned from activity implementations with message and optional details.
	ApplicationError TemporalErrorType = 0

	// CancelledError indicates that an operation was cancelled
	CancelledError TemporalErrorType = 1

	// ActivityError returned from workflow when activity returned an error.
	ActivityError TemporalErrorType = 2

	// ServerError can be returned from server.
	ServerError TemporalErrorType = 3

	// ChildWorkflowExecutionError returned from workflow when child workflow returned an error.
	ChildWorkflowExecutionError TemporalErrorType = 4

	// WorkflowExecutionError returned from workflow.
	WorkflowExecutionError TemporalErrorType = 5

	// TimeoutError is a timeout error
	TimeoutError TemporalErrorType = 6

	// TerminatedError is a termination error
	TerminatedError TemporalErrorType = 7

	// PanicError is a panic error
	PanicError TemporalErrorType = 8

	// UnknownExternalWorkflowExecutionError can be returned when external workflow doesn't exist
	UnknownExternalWorkflowExecutionError TemporalErrorType = 9
)

type (

	// TemporalError is a struct used to pass errors
	// generated by calls to the temporal server from the
	// temporal-proxy to the Neon.Temporal Library.
	TemporalError struct {
		String *string `json:"String"`
		Type   *string `json:"Type"`
	}

	// TemporalErrorType is an enumerated list of
	// all of the temporal error types
	TemporalErrorType int
)

var (
	// ErrConnection is the custom error that is thrown when the temporal-proxy
	// is not able to establish a connection with the temporal server
	ErrConnection = errors.New("TemporalConnectionError{Message: Could not establish a connection with the temporal server.}")

	// ErrEntityNotExist is the custom error that is thrown when a temporal
	// entity cannot be found in the temporal server
	ErrEntityNotExist = errors.New("EntityNotExistsError{Message: The entity you are looking for does not exist.}")

	// ErrArgumentNil is the custom error that is thrown when trying to access a nil
	// value
	ErrArgumentNil = errors.New("ArgumentNilError{Message: failed to access nil value.}")
)

// NewTemporalError is the constructor for a TemporalError
// when supplied parameters.
//
// param err error -> error to set.
//
// param errorType ...interface{} -> the temporal error type.
func NewTemporalError(err error, errTypes ...TemporalErrorType) *TemporalError {
	if err == nil || reflect.ValueOf(err).IsNil() {
		return nil
	}

	if v, ok := err.(*TemporalError); ok {
		return v
	}

	var errType TemporalErrorType
	if len(errTypes) > 0 {
		errType = errTypes[0]
	} else {
		if temporal.IsCanceledError(err) {
			errType = CancelledError
		} else if temporal.IsApplicationError(err) {
			errType = ApplicationError
		} else if temporal.IsPanicError(err) {
			errType = PanicError
		} else if temporal.IsTerminatedError(err) {
			errType = TerminatedError
		} else if temporal.IsTimeoutError(err) {
			errType = TimeoutError
		} else {
			errType = ApplicationError
		}
	}

	errStr := err.Error()
	errTypeStr := errType.String()

	return &TemporalError{String: &errStr, Type: &errTypeStr}
}

func (c *TemporalError) Error() string {
	if c.String == nil {
		return ""
	}

	return *c.String
}

// ToError returns an error interface from a TemporalError
// instance.  If the TemporalError is of type ApplicationError or
// Canceled, the resulting Temporal client errors will
// be returned.
//
// error -> the TemporalError as an error interface.
func (c *TemporalError) ToError() error {
	var err error
	errType := c.GetType()
	switch errType {
	case ApplicationError:
		err = temporal.NewApplicationError(c.Error(), "")
		break
	case CancelledError:
		err = temporal.NewCanceledError(c.Error())
	default:
		return c
	}

	return err
}

// GetType gets the TemporalErrorType from a TemporalError
// instance.
//
// returns TemporalErrorType -> the corresponding error type to the
// string representing the error type in a TemporalError instance
func (c *TemporalError) GetType() TemporalErrorType {
	if c.Type == nil {
		err := fmt.Errorf("no error type set")
		panic(err)
	}

	switch *c.Type {
	case "cancelled":
		return CancelledError
	case "custom":
		return ApplicationError
	case "generic":
		return GenericError
	case "panic":
		return PanicError
	case "terminated":
		return TerminatedError
	case "timeout":
		return TimeoutError
	default:
		err := fmt.Errorf("unrecognized error type %v", *c.Type)
		panic(err)
	}
}

// SetType sets the *string to the corresponding TemporalErrorType
// in a TemporalError instance
//
// param errorType TemporalErrorType -> the TemporalErrorType to set as a string
// in a TemporalError instance
func (c *TemporalError) SetType(errorType TemporalErrorType) {
	var typeString string
	switch errorType {
	case CancelledError:
		typeString = "cancelled"
	case ApplicationError:
		typeString = "custom"
	case GenericError:
		typeString = "generic"
	case PanicError:
		typeString = "panic"
	case TerminatedError:
		typeString = "terminated"
	case TimeoutError:
		typeString = "timeout"
	default:
		err := fmt.Errorf("unrecognized error type %v", errorType)
		panic(err)
	}
	c.Type = &typeString
}

func (t *TemporalErrorType) String() string {
	return [...]string{
		"cancelled",
		"custom",
		"generic",
		"panic",
		"terminated",
		"timeout",
	}[*t]
}

// IsApplicationError determines if an error
// is a TemporalError of type ApplicationError.
//
// param err error -> the error to evaluate.
//
// returns bool -> error is a TemporalError of type
// ApplicationError or not.
func IsApplicationError(err error) bool {
	if err != nil && !reflect.ValueOf(err).IsNil() {
		if v, ok := err.(*TemporalError); ok {
			if v.GetType() == ApplicationError {
				return true
			}
		} else {
			return temporal.IsApplicationError(err)
		}
	}

	return false
}

// IsCancelledError determines if an error
// is a TemporalError of type CancelledError.
//
// param err error -> the error to evaluate.
//
// returns bool -> error is a TemporalError of type
// CancelledError or not.
func IsCancelledError(err error) bool {
	if err != nil && !reflect.ValueOf(err).IsNil() {
		if v, ok := err.(*TemporalError); ok {
			if v.GetType() == CancelledError {
				return true
			}
		} else {
			return temporal.IsCanceledError(err)
		}
	}

	return false
}

// IsGenericError determines if an error
// is a TemporalError of type GenericError.
//
// param err error -> the error to evaluate.
//
// returns bool -> error is a TemporalError of type
// GenericError or not.
func IsGenericError(err error) bool {
	if err != nil && !reflect.ValueOf(err).IsNil() {
		if v, ok := err.(*TemporalError); ok {
			if v.GetType() == GenericError {
				return true
			}
		} else {
			return temporal.IsGenericError(err)
		}
	}

	return false
}

// IsPanicError determines if an error
// is a TemporalError of type PanicError.
//
// param err error -> the error to evaluate.
//
// returns bool -> error is a TemporalError of type
// PanicError or not.
func IsPanicError(err error) bool {
	if err != nil && !reflect.ValueOf(err).IsNil() {
		if v, ok := err.(*TemporalError); ok {
			if v.GetType() == PanicError {
				return true
			}
		} else {
			return temporal.IsPanicError(err)
		}
	}

	return false
}

// IsTerminatedError determines if an error
// is a TemporalError of type TerminatedError.
//
// param err error -> the error to evaluate.
//
// returns bool -> error is a TemporalError of type
// TerminatedError or not.
func IsTerminatedError(err error) bool {
	if err != nil && !reflect.ValueOf(err).IsNil() {
		if v, ok := err.(*TemporalError); ok {
			if v.GetType() == TerminatedError {
				return true
			}
		} else {
			return temporal.IsTerminatedError(err)
		}
	}

	return false
}

// IsTimeoutError determines if an error
// is a TemporalError of type TimeoutError.
//
// param err error -> the error to evaluate.
//
// returns bool -> error is a TemporalError of type
// TimeoutError or not.
func IsTimeoutError(err error) bool {
	if err != nil && !reflect.ValueOf(err).IsNil() {
		if v, ok := err.(*TemporalError); ok {
			if v.GetType() == TimeoutError {
				return true
			}
		} else {
			return temporal.IsTimeoutError(err)
		}
	}

	return false
}
