//-----------------------------------------------------------------------------
// FILE:		proxy_request_helper.go
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

package endpoints

import (
	"bytes"
	"context"
	"errors"
	"fmt"
	"net/http"
	"os"
	"reflect"
	"time"

	cadenceshared "go.uber.org/cadence/.gen/go/shared"
	"go.uber.org/cadence/activity"
	"go.uber.org/cadence/client"
	"go.uber.org/cadence/worker"
	"go.uber.org/cadence/workflow"
	"go.uber.org/zap"

	"github.com/cadence-proxy/internal/cadence/cadenceactivities"
	cadenceclient "github.com/cadence-proxy/internal/cadence/cadenceclient"
	"github.com/cadence-proxy/internal/cadence/cadenceerrors"
	"github.com/cadence-proxy/internal/cadence/cadenceworkers"
	"github.com/cadence-proxy/internal/cadence/cadenceworkflows"
	"github.com/cadence-proxy/internal/messages"
	messagetypes "github.com/cadence-proxy/internal/messages/types"
)

var (

	// ClientHelper is a global variable that holds this cadence-proxy's instance
	// of the CadenceClientHelper that will be used to create domain and workflow clients
	// that communicate with the cadence server
	clientHelper = cadenceclient.NewCadenceClientHelper()
)

// -------------------------------------------------------------------------
// IProxyRequest message type handler entrypoint

func handleIProxyRequest(request messages.IProxyRequest) error {

	// error for catching exceptions in the switch block
	var err error
	var reply messages.IProxyReply

	// handle the messages individually based on their message type
	switch request.GetType() {

	// -------------------------------------------------------------------------
	// Client message types

	// InitializeRequest
	case messagetypes.InitializeRequest:
		if v, ok := request.(*messages.InitializeRequest); ok {
			reply = handleInitializeRequest(v)
		}

	// HeartbeatRequest
	case messagetypes.HeartbeatRequest:
		if v, ok := request.(*messages.HeartbeatRequest); ok {
			reply = handleHeartbeatRequest(v)
		}

	// CancelRequest
	case messagetypes.CancelRequest:
		if v, ok := request.(*messages.CancelRequest); ok {
			reply = handleCancelRequest(v)
		}

	// ConnectRequest
	case messagetypes.ConnectRequest:
		if v, ok := request.(*messages.ConnectRequest); ok {
			reply = handleConnectRequest(v)
		}

	// DomainDescribeRequest
	case messagetypes.DomainDescribeRequest:
		if v, ok := request.(*messages.DomainDescribeRequest); ok {
			reply = handleDomainDescribeRequest(v)
		}

	// DomainRegisterRequest
	case messagetypes.DomainRegisterRequest:
		if v, ok := request.(*messages.DomainRegisterRequest); ok {
			reply = handleDomainRegisterRequest(v)
		}

	// DomainUpdateRequest
	case messagetypes.DomainUpdateRequest:
		if v, ok := request.(*messages.DomainUpdateRequest); ok {
			reply = handleDomainUpdateRequest(v)
		}

	// TerminateRequest
	case messagetypes.TerminateRequest:
		if v, ok := request.(*messages.TerminateRequest); ok {
			reply = handleTerminateRequest(v)
		}

	// NewWorkerRequest
	case messagetypes.NewWorkerRequest:
		if v, ok := request.(*messages.NewWorkerRequest); ok {
			reply = handleNewWorkerRequest(v)
		}

	// StopWorkerRequest
	case messagetypes.StopWorkerRequest:
		if v, ok := request.(*messages.StopWorkerRequest); ok {
			reply = handleStopWorkerRequest(v)
		}

	// PingRequest
	case messagetypes.PingRequest:
		if v, ok := request.(*messages.PingRequest); ok {
			reply = handlePingRequest(v)
		}

	// -------------------------------------------------------------------------
	// Workflow message types

	// WorkflowRegisterRequest
	case messagetypes.WorkflowRegisterRequest:
		if v, ok := request.(*messages.WorkflowRegisterRequest); ok {
			reply = handleWorkflowRegisterRequest(v)
		}

	// WorkflowExecuteRequest
	case messagetypes.WorkflowExecuteRequest:
		if v, ok := request.(*messages.WorkflowExecuteRequest); ok {
			reply = handleWorkflowExecuteRequest(v)
		}

	// WorkflowCancelRequest
	case messagetypes.WorkflowCancelRequest:
		if v, ok := request.(*messages.WorkflowCancelRequest); ok {
			reply = handleWorkflowCancelRequest(v)
		}

	// WorkflowTerminateRequest
	case messagetypes.WorkflowTerminateRequest:
		if v, ok := request.(*messages.WorkflowTerminateRequest); ok {
			reply = handleWorkflowTerminateRequest(v)
		}

	// WorkflowSignalWithStartRequest
	case messagetypes.WorkflowSignalWithStartRequest:
		if v, ok := request.(*messages.WorkflowSignalWithStartRequest); ok {
			reply = handleWorkflowSignalWithStartRequest(v)
		}

	// WorkflowSetCacheSizeRequest
	case messagetypes.WorkflowSetCacheSizeRequest:
		if v, ok := request.(*messages.WorkflowSetCacheSizeRequest); ok {
			reply = handleWorkflowSetCacheSizeRequest(v)
		}

	// WorkflowQueryRequest
	case messagetypes.WorkflowQueryRequest:
		if v, ok := request.(*messages.WorkflowQueryRequest); ok {
			reply = handleWorkflowQueryRequest(v)
		}

	// WorkflowMutableRequest
	case messagetypes.WorkflowMutableRequest:
		if v, ok := request.(*messages.WorkflowMutableRequest); ok {
			reply = handleWorkflowMutableRequest(v)
		}

	// WorkflowDescribeExecutionRequest
	case messagetypes.WorkflowDescribeExecutionRequest:
		if v, ok := request.(*messages.WorkflowDescribeExecutionRequest); ok {
			reply = handleWorkflowDescribeExecutionRequest(v)
		}

	// WorkflowGetResultRequest
	case messagetypes.WorkflowGetResultRequest:
		if v, ok := request.(*messages.WorkflowGetResultRequest); ok {
			reply = handleWorkflowGetResultRequest(v)
		}

	// WorkflowSignalSubscribeRequest
	case messagetypes.WorkflowSignalSubscribeRequest:
		if v, ok := request.(*messages.WorkflowSignalSubscribeRequest); ok {
			reply = handleWorkflowSignalSubscribeRequest(v)
		}

	// WorkflowSignalRequest
	case messagetypes.WorkflowSignalRequest:
		if v, ok := request.(*messages.WorkflowSignalRequest); ok {
			reply = handleWorkflowSignalRequest(v)
		}

	// WorkflowHasLastResultRequest
	case messagetypes.WorkflowHasLastResultRequest:
		if v, ok := request.(*messages.WorkflowHasLastResultRequest); ok {
			reply = handleWorkflowHasLastResultRequest(v)
		}

	// WorkflowGetLastResultRequest
	case messagetypes.WorkflowGetLastResultRequest:
		if v, ok := request.(*messages.WorkflowGetLastResultRequest); ok {
			reply = handleWorkflowGetLastResultRequest(v)
		}

	// WorkflowDisconnectContextRequest
	case messagetypes.WorkflowDisconnectContextRequest:
		if v, ok := request.(*messages.WorkflowDisconnectContextRequest); ok {
			reply = handleWorkflowDisconnectContextRequest(v)
		}

	// WorkflowGetTimeRequest
	case messagetypes.WorkflowGetTimeRequest:
		if v, ok := request.(*messages.WorkflowGetTimeRequest); ok {
			reply = handleWorkflowGetTimeRequest(v)
		}

	// WorkflowSleepRequest
	case messagetypes.WorkflowSleepRequest:
		if v, ok := request.(*messages.WorkflowSleepRequest); ok {
			reply = handleWorkflowSleepRequest(v)
		}

	// WorkflowExecuteChildRequest
	case messagetypes.WorkflowExecuteChildRequest:
		if v, ok := request.(*messages.WorkflowExecuteChildRequest); ok {
			reply = handleWorkflowExecuteChildRequest(v)
		}

	// WorkflowWaitForChildRequest
	case messagetypes.WorkflowWaitForChildRequest:
		if v, ok := request.(*messages.WorkflowWaitForChildRequest); ok {
			reply = handleWorkflowWaitForChildRequest(v)
		}

	// WorkflowSignalChildRequest
	case messagetypes.WorkflowSignalChildRequest:
		if v, ok := request.(*messages.WorkflowSignalChildRequest); ok {
			reply = handleWorkflowSignalChildRequest(v)
		}

	// WorkflowCancelChildRequest
	case messagetypes.WorkflowCancelChildRequest:
		if v, ok := request.(*messages.WorkflowCancelChildRequest); ok {
			reply = handleWorkflowCancelChildRequest(v)
		}

	// WorkflowSetQueryHandlerRequest
	case messagetypes.WorkflowSetQueryHandlerRequest:
		if v, ok := request.(*messages.WorkflowSetQueryHandlerRequest); ok {
			reply = handleWorkflowSetQueryHandlerRequest(v)
		}

	// -------------------------------------------------------------------------
	// Activity message types

	// ActivityExecuteRequest
	case messagetypes.ActivityExecuteRequest:
		if v, ok := request.(*messages.ActivityExecuteRequest); ok {
			reply = handleActivityExecuteRequest(v)
		}

	// ActivityRegisterRequest
	case messagetypes.ActivityRegisterRequest:
		if v, ok := request.(*messages.ActivityRegisterRequest); ok {
			reply = handleActivityRegisterRequest(v)
		}

	// ActivityHasHeartbeatDetailsRequest
	case messagetypes.ActivityHasHeartbeatDetailsRequest:
		if v, ok := request.(*messages.ActivityHasHeartbeatDetailsRequest); ok {
			reply = handleActivityHasHeartbeatDetailsRequest(v)
		}

	// ActivityGetHeartbeatDetailsRequest
	case messagetypes.ActivityGetHeartbeatDetailsRequest:
		if v, ok := request.(*messages.ActivityGetHeartbeatDetailsRequest); ok {
			reply = handleActivityGetHeartbeatDetailsRequest(v)
		}

	// ActivityRecordHeartbeatRequest
	case messagetypes.ActivityRecordHeartbeatRequest:
		if v, ok := request.(*messages.ActivityRecordHeartbeatRequest); ok {
			reply = handleActivityRecordHeartbeatRequest(v)
		}

	// ActivityGetInfoRequest
	case messagetypes.ActivityGetInfoRequest:
		if v, ok := request.(*messages.ActivityGetInfoRequest); ok {
			reply = handleActivityGetInfoRequest(v)
		}

	// ActivityCompleteRequest
	case messagetypes.ActivityCompleteRequest:
		if v, ok := request.(*messages.ActivityCompleteRequest); ok {
			reply = handleActivityCompleteRequest(v)
		}

	// ActivityExecuteLocalRequest
	case messagetypes.ActivityExecuteLocalRequest:
		if v, ok := request.(*messages.ActivityExecuteLocalRequest); ok {
			reply = handleActivityExecuteLocalRequest(v)
		}

	// Undefined message type
	default:

		err = fmt.Errorf("unhandled message type. could not complete type assertion for type %d", request.GetType())

		// $debug(jack.burns): DELETE THIS!
		logger.Debug("Unhandled message type. Could not complete type assertion", zap.Error(err))
	}

	// catch any errors that may have occurred
	// in the switch block or if the message could not
	// be cast to a specific type
	if (err != nil) || (reflect.ValueOf(reply).IsNil()) {
		return err
	}

	// send the reply as an http.Request back to the Neon.Cadence Library
	// via http.PUT
	resp, err := putToNeonCadenceClient(reply)
	if err != nil {
		return err
	}
	defer func() {

		// $debug(jack.burns): DELETE THIS!
		err := resp.Body.Close()
		if err != nil {
			logger.Error("could not close response body", zap.Error(err))
		}
	}()

	return nil
}

// -------------------------------------------------------------------------
// IProxyRequest client message type handler methods

func handleCancelRequest(request *messages.CancelRequest) messages.IProxyReply {

	// $debug(jack.burns): DELETE THIS!
	logger.Debug("CancelRequest Received", zap.Int("ProccessId", os.Getpid()))

	// new InitializeReply
	reply := createReplyMessage(request)
	buildReply(reply, nil, true)

	return reply
}

func handleConnectRequest(request *messages.ConnectRequest) messages.IProxyReply {

	// $debug(jack.burns): DELETE THIS!
	logger.Debug("ConnectRequest Received", zap.Int("ProccessId", os.Getpid()))

	// new ConnectReply
	reply := createReplyMessage(request)

	// client options
	opts := client.Options{
		Identity: *request.GetIdentity(),
	}

	// setup the CadenceClientHelper
	clientHelper = cadenceclient.NewCadenceClientHelper()
	clientHelper.SetHostPort(*request.GetEndpoints())
	clientHelper.SetClientOptions(&opts)

	err := clientHelper.SetupServiceConfig()
	if err != nil {

		// set the client helper to nil indicating that
		// there is no connection that has been made to the cadence
		// server
		clientHelper = nil

		// build the rest of the reply with a custom error
		buildReply(reply, cadenceerrors.NewCadenceError(err.Error()))

		return reply
	}

	// make a channel that waits for a connection to be established
	// until returning ready
	connectChan := make(chan error)
	ctx, cancel := context.WithTimeout(context.Background(), cadenceClientTimeout)

	// defer the cancel of the context and
	// closing of the connectChan
	defer func() {
		cancel()
		close(connectChan)
	}()

	go func() {

		// build the domain client using a configured CadenceClientHelper instance
		domainClient, err := clientHelper.Builder.BuildCadenceDomainClient()
		if err != nil {
			connectChan <- err
			return
		}

		// send a describe domain request to the cadence server
		_, err = domainClient.Describe(ctx, _cadenceSystemDomain)
		if err != nil {
			connectChan <- err
			return
		}

		connectChan <- nil
	}()

	connectResult := <-connectChan
	if connectResult != nil {
		clientHelper = nil
		buildReply(reply, cadenceerrors.NewCadenceError(errConnection.Error()))

		return reply
	}

	// set the timeout
	cadenceClientTimeout = request.GetClientTimeout()

	// build the reply
	buildReply(reply, nil)

	return reply
}

func handleDomainDescribeRequest(request *messages.DomainDescribeRequest) messages.IProxyReply {

	// $debug(jack.burns): DELETE THIS!
	logger.Debug("DomainDescribeRequest Received", zap.Int("ProccessId", os.Getpid()))

	// new DomainDescribeReply
	reply := createReplyMessage(request)

	// check to see if a connection has been made with the
	// cadence client
	if clientHelper == nil {
		buildReply(reply, cadenceerrors.NewCadenceError(errConnection.Error()))

		return reply
	}

	// build the domain client using a configured CadenceClientHelper instance
	domainClient, err := clientHelper.Builder.BuildCadenceDomainClient()
	if err != nil {
		buildReply(reply, cadenceerrors.NewCadenceError(err.Error()))

		return reply
	}

	// send a describe domain request to the cadence server
	describeDomainResponse, err := domainClient.Describe(context.Background(), *request.GetName())
	if err != nil {
		buildReply(reply, cadenceerrors.NewCadenceError(err.Error()))

		return reply
	}

	buildReply(reply, nil, describeDomainResponse)

	return reply
}

func handleDomainRegisterRequest(request *messages.DomainRegisterRequest) messages.IProxyReply {

	// $debug(jack.burns): DELETE THIS!
	logger.Debug("DomainRegisterRequest Received", zap.Int("ProccessId", os.Getpid()))

	// new DomainRegisterReply
	reply := createReplyMessage(request)

	// check to see if a connection has been made with the
	// cadence client
	if clientHelper == nil {
		buildReply(reply, cadenceerrors.NewCadenceError(errConnection.Error()))

		return reply
	}

	// build the domain client using a configured CadenceClientHelper instance
	domainClient, err := clientHelper.Builder.BuildCadenceDomainClient()
	if err != nil {
		buildReply(reply, cadenceerrors.NewCadenceError(err.Error()))

		return reply
	}

	// create a new cadence domain RegisterDomainRequest for
	// registering a new domain
	emitMetrics := request.GetEmitMetrics()
	retentionDays := request.GetRetentionDays()
	domainRegisterRequest := cadenceshared.RegisterDomainRequest{
		Name:                                   request.GetName(),
		Description:                            request.GetDescription(),
		OwnerEmail:                             request.GetOwnerEmail(),
		EmitMetric:                             &emitMetrics,
		WorkflowExecutionRetentionPeriodInDays: &retentionDays,
	}

	// register the domain using the RegisterDomainRequest
	err = domainClient.Register(context.Background(), &domainRegisterRequest)
	if err != nil {
		buildReply(reply, cadenceerrors.NewCadenceError(err.Error()))

		return reply
	}

	// $debug(jack.burns): DELETE THIS!
	logger.Debug("domain successfully registered", zap.String("Domain Name", domainRegisterRequest.GetName()))
	buildReply(reply, nil)

	return reply
}

func handleDomainUpdateRequest(request *messages.DomainUpdateRequest) messages.IProxyReply {

	// $debug(jack.burns): DELETE THIS!
	logger.Debug("DomainUpdateRequest Received", zap.Int("ProccessId", os.Getpid()))

	// new DomainUpdateReply
	reply := createReplyMessage(request)

	// check to see if a connection has been made with the
	// cadence client
	if clientHelper == nil {
		buildReply(reply, cadenceerrors.NewCadenceError(errConnection.Error()))

		return reply
	}

	// build the domain client using a configured CadenceClientHelper instance
	domainClient, err := clientHelper.Builder.BuildCadenceDomainClient()
	if err != nil {
		buildReply(reply, cadenceerrors.NewCadenceError(err.Error()))

		return reply
	}

	// DomainUpdateRequest.Configuration
	configuration := new(cadenceshared.DomainConfiguration)
	configurationEmitMetrics := request.GetConfigurationEmitMetrics()
	configurationRetentionDays := request.GetConfigurationRetentionDays()
	configuration.EmitMetric = &configurationEmitMetrics
	configuration.WorkflowExecutionRetentionPeriodInDays = &configurationRetentionDays

	// DomainUpdateRequest.UpdatedInfo
	updatedInfo := new(cadenceshared.UpdateDomainInfo)
	updatedInfo.Description = request.GetUpdatedInfoDescription()
	updatedInfo.OwnerEmail = request.GetUpdatedInfoOwnerEmail()

	// DomainUpdateRequest
	domainUpdateRequest := cadenceshared.UpdateDomainRequest{
		Name:          request.GetName(),
		Configuration: configuration,
		UpdatedInfo:   updatedInfo,
	}

	// Update the domain using the UpdateDomainRequest
	err = domainClient.Update(context.Background(), &domainUpdateRequest)
	if err != nil {
		buildReply(reply, cadenceerrors.NewCadenceError(err.Error()))

		return reply
	}

	// $debug(jack.burns): DELETE THIS!
	logger.Debug("domain successfully Updated", zap.String("Domain Name", domainUpdateRequest.GetName()))
	buildReply(reply, nil)

	return reply
}

func handleHeartbeatRequest(request *messages.HeartbeatRequest) messages.IProxyReply {

	// $debug(jack.burns): DELETE THIS!
	logger.Debug("HeartbeatRequest Received", zap.Int("ProccessId", os.Getpid()))

	// new HeartbeatReply
	reply := createReplyMessage(request)
	buildReply(reply, nil)

	return reply
}

func handleInitializeRequest(request *messages.InitializeRequest) messages.IProxyReply {

	// $debug(jack.burns): DELETE THIS!
	logger.Debug("InitializeRequest Received", zap.Int("ProccessId", os.Getpid()))

	// new InitializeReply
	reply := createReplyMessage(request)

	// get the port and address from the InitializeRequest
	address := *request.GetLibraryAddress()
	port := request.GetLibraryPort()
	replyAddress = fmt.Sprintf("http://%s:%d/",
		address,
		port,
	)

	// $debug(jack.burns): DELETE THIS!
	if DebugPrelaunched {
		replyAddress = "http://127.0.0.2:5001/"
	}

	// $debug(jack.burns): DELETE THIS!
	logger.Debug("InitializeRequest info",
		zap.String("Library Address", address),
		zap.Int32("LibaryPort", port),
		zap.String("Reply Address", replyAddress),
	)

	// build the reply
	buildReply(reply, nil)

	return reply
}

func handleTerminateRequest(request *messages.TerminateRequest) messages.IProxyReply {

	// $debug(jack.burns): DELETE THIS!
	logger.Debug("TerminateRequest Received", zap.Int("ProccessId", os.Getpid()))

	// new TerminateReply
	reply := createReplyMessage(request)

	// setting terminate to true indicates that after the TerminateReply is sent,
	// the server instance should gracefully shut down
	terminate = true

	// build the reply
	buildReply(reply, nil)

	return reply
}

func handleNewWorkerRequest(request *messages.NewWorkerRequest) messages.IProxyReply {

	// $debug(jack.burns): DELETE THIS!
	logger.Debug("NewWorkerRequest Received", zap.Int("ProccessId", os.Getpid()))

	// new NewWorkerReply
	reply := createReplyMessage(request)

	// check to see if a connection has been made with the
	// cadence client
	if clientHelper == nil {
		buildReply(reply, cadenceerrors.NewCadenceError(errConnection.Error()))

		return reply
	}

	// create a new worker using a configured CadenceClientHelper instance
	workerID := cadenceworkers.NextWorkerID()
	worker, err := clientHelper.StartWorker(*request.GetDomain(),
		*request.GetTaskList(),
		*request.GetOptions(),
	)

	if err != nil {
		buildReply(reply, cadenceerrors.NewCadenceError(err.Error()), workerID)

		return reply
	}

	// put the worker and workerID from the new worker to the
	workerID = cadenceworkers.Workers.Add(workerID, worker)

	// build the reply
	buildReply(reply, nil, workerID)

	return reply
}

func handleStopWorkerRequest(request *messages.StopWorkerRequest) messages.IProxyReply {

	// $debug(jack.burns): DELETE THIS!
	logger.Debug("StopWorkerRequest Received", zap.Int("ProccessId", os.Getpid()))

	// new StopWorkerReply
	reply := createReplyMessage(request)

	// check to see if a connection has been made with the
	// cadence client
	if clientHelper == nil {
		buildReply(reply, cadenceerrors.NewCadenceError(errConnection.Error()))

		return reply
	}

	// get the workerID from the request so that we know
	// what worker to stop
	workerID := request.GetWorkerID()
	worker := cadenceworkers.Workers.Get(workerID)
	if worker == nil {
		buildReply(reply, cadenceerrors.NewCadenceError(errEntityNotExist.Error()))

		return reply
	}

	// stop the worker and
	// remove it from the cadenceworkers.Workers map
	worker.Stop()
	workerID = cadenceworkers.Workers.Remove(workerID)

	// $debug(jack.burns): DELETE THIS!
	logger.Debug("Worker has been deleted", zap.Int64("WorkerID", workerID))

	// build the reply
	buildReply(reply, nil)

	return reply
}

func handlePingRequest(request *messages.PingRequest) messages.IProxyReply {

	// $debug(jack.burns): DELETE THIS!
	logger.Debug("PingRequest Received", zap.Int("ProccessId", os.Getpid()))

	// new PingReply
	reply := createReplyMessage(request)
	buildReply(reply, nil)

	return reply
}

// -------------------------------------------------------------------------
// IProxyRequest workflow message type handler methods

func handleWorkflowRegisterRequest(request *messages.WorkflowRegisterRequest) messages.IProxyReply {

	// $debug(jack.burns): DELETE THIS!
	logger.Debug("WorkflowRegisterRequest Received", zap.Int("ProccessId", os.Getpid()))

	// new WorkflowRegisterReply
	reply := createReplyMessage(request)

	// check to see if a connection has been made with the
	// cadence client
	if clientHelper == nil {
		buildReply(reply, cadenceerrors.NewCadenceError(errConnection.Error()))

		return reply
	}

	// create workflow function
	workflowName := request.GetName()
	workflowFunc := func(ctx workflow.Context, input []byte) ([]byte, error) {

		// new WorkflowContext
		contextID := cadenceworkflows.NextContextID()
		wectx := cadenceworkflows.NewWorkflowContext(ctx)
		wectx.SetWorkflowName(workflowName)

		// set the WorkflowContext in WorkflowContexts
		contextID = cadenceworkflows.WorkflowContexts.Add(contextID, wectx)

		// Send a WorkflowInvokeRequest to the Neon.Cadence Lib
		// cadence-client
		requestID := NextRequestID()
		workflowInvokeRequest := messages.NewWorkflowInvokeRequest()
		workflowInvokeRequest.SetRequestID(requestID)
		workflowInvokeRequest.SetContextID(contextID)
		workflowInvokeRequest.SetArgs(input)

		// get the WorkflowInfo (Domain, WorkflowID, RunID, WorkflowType,
		// TaskList, ExecutionStartToCloseTimeout)
		// from the context
		workflowInfo := workflow.GetInfo(ctx)
		workflowInvokeRequest.SetDomain(&workflowInfo.Domain)
		workflowInvokeRequest.SetWorkflowID(&workflowInfo.WorkflowExecution.ID)
		workflowInvokeRequest.SetRunID(&workflowInfo.WorkflowExecution.RunID)
		workflowInvokeRequest.SetWorkflowType(&workflowInfo.WorkflowType.Name)
		workflowInvokeRequest.SetTaskList(&workflowInfo.TaskListName)
		workflowInvokeRequest.SetExecutionStartToCloseTimeout(time.Duration(int64(workflowInfo.ExecutionStartToCloseTimeoutSeconds) * int64(time.Second)))

		// create the Operation for this request and add it to the operations map
		op := NewOperation(requestID, workflowInvokeRequest)
		op.SetChannel(make(chan interface{}))
		op.SetContextID(contextID)
		Operations.Add(requestID, op)

		// send the WorkflowInvokeRequest
		resp, err := putToNeonCadenceClient(workflowInvokeRequest)
		if err != nil {
			panic(err)
		}
		defer func() {

			// $debug(jack.burns): DELETE THIS!
			err := resp.Body.Close()
			if err != nil {
				logger.Error("could not close response body", zap.Error(err))
			}
		}()

		// check if the result is an or
		// a []byte result
		result := <-op.GetChannel()
		switch s := result.(type) {
		case error:
			return nil, s
		case []byte:
			return s, nil
		default:
			return nil, fmt.Errorf("unexpected result type %v.  result must be an error or []byte", reflect.TypeOf(s))
		}
	}

	// register the workflow
	workflow.RegisterWithOptions(workflowFunc, workflow.RegisterOptions{Name: *workflowName})

	// build the reply
	// $debug(jack.burns): DELETE THIS!
	logger.Debug("Workflow Successfully Registered", zap.String("WorkflowName", *workflowName))
	buildReply(reply, nil)

	return reply
}

func handleWorkflowExecuteRequest(request *messages.WorkflowExecuteRequest) messages.IProxyReply {

	// $debug(jack.burns): DELETE THIS!
	logger.Debug("WorkflowExecuteRequest Received", zap.Int("ProccessId", os.Getpid()))

	// new WorkflowExecuteReply
	reply := createReplyMessage(request)

	// check to see if a connection has been made with the
	// cadence client
	if clientHelper == nil {
		buildReply(reply, cadenceerrors.NewCadenceError(errConnection.Error()))

		return reply
	}

	// create the context
	ctx, cancel := context.WithTimeout(context.Background(), cadenceClientTimeout)
	defer cancel()

	// check for options
	var opts client.StartWorkflowOptions
	if v := request.GetOptions(); v != nil {
		opts = *v
		if opts.DecisionTaskStartToCloseTimeout <= 0 {
			opts.DecisionTaskStartToCloseTimeout = cadenceClientTimeout
		}
	} else {
		opts = client.StartWorkflowOptions{
			ExecutionStartToCloseTimeout:    cadenceClientTimeout,
			DecisionTaskStartToCloseTimeout: cadenceClientTimeout,
		}
	}

	// signalwithstart the specified workflow
	workflowRun, err := clientHelper.ExecuteWorkflow(ctx,
		*request.GetDomain(),
		opts,
		*request.GetWorkflow(),
		request.GetArgs(),
	)

	if err != nil {
		buildReply(reply, cadenceerrors.NewCadenceError(err.Error()))

		return reply
	}

	// extract the workflow ID and RunID
	workflowExecution := workflow.Execution{
		ID:    workflowRun.GetID(),
		RunID: workflowRun.GetRunID(),
	}

	// build the reply
	buildReply(reply, nil, workflowExecution)

	return reply
}

func handleWorkflowCancelRequest(request *messages.WorkflowCancelRequest) messages.IProxyReply {

	// $debug(jack.burns): DELETE THIS!
	logger.Debug("WorkflowCancelRequest Received", zap.Int("ProccessId", os.Getpid()))

	// new WorkflowCancelReply
	reply := createReplyMessage(request)

	// check to see if a connection has been made with the
	// cadence client
	if clientHelper == nil {
		buildReply(reply, cadenceerrors.NewCadenceError(errConnection.Error()))

		return reply
	}

	// build the cadence client using a configured CadenceClientHelper instance
	client, err := clientHelper.Builder.BuildCadenceClient()
	if err != nil {
		buildReply(reply, cadenceerrors.NewCadenceError(err.Error()))

		return reply
	}

	// create the context to cancel the workflow
	ctx, cancel := context.WithTimeout(context.Background(), cadenceClientTimeout)
	defer cancel()

	// cancel the specified workflow
	err = client.CancelWorkflow(ctx,
		*request.GetWorkflowID(),
		*request.GetRunID(),
	)

	if err != nil {
		buildReply(reply, cadenceerrors.NewCadenceError(err.Error()))

		return reply
	}

	// build the reply
	buildReply(reply, nil)

	return reply
}

func handleWorkflowTerminateRequest(request *messages.WorkflowTerminateRequest) messages.IProxyReply {

	// $debug(jack.burns): DELETE THIS!
	logger.Debug("WorkflowTerminateRequest Received", zap.Int("ProccessId", os.Getpid()))

	// new WorkflowTerminateReply
	reply := createReplyMessage(request)

	// check to see if a connection has been made with the
	// cadence client
	if clientHelper == nil {
		buildReply(reply, cadenceerrors.NewCadenceError(errConnection.Error()))

		return reply
	}

	// build the cadence client using a configured CadenceClientHelper instance
	client, err := clientHelper.Builder.BuildCadenceClient()
	if err != nil {
		buildReply(reply, cadenceerrors.NewCadenceError(err.Error()))

		return reply
	}

	// create the context to terminate the workflow
	ctx, cancel := context.WithTimeout(context.Background(), cadenceClientTimeout)
	defer cancel()

	// terminate the specified workflow
	err = client.TerminateWorkflow(ctx,
		*request.GetWorkflowID(),
		*request.GetRunID(),
		*request.GetReason(),
		request.GetDetails(),
	)

	if err != nil {
		buildReply(reply, cadenceerrors.NewCadenceError(err.Error()))

		return reply
	}

	// build the reply
	buildReply(reply, nil)

	return reply
}

func handleWorkflowSignalWithStartRequest(request *messages.WorkflowSignalWithStartRequest) messages.IProxyReply {

	// $debug(jack.burns): DELETE THIS!
	logger.Debug("WorkflowSignalWithStartRequest Received", zap.Int("ProccessId", os.Getpid()))

	// new WorkflowSignalWithStartReply
	reply := createReplyMessage(request)

	// check to see if a connection has been made with the
	// cadence client
	if clientHelper == nil {
		buildReply(reply, cadenceerrors.NewCadenceError(errConnection.Error()))

		return reply
	}

	// build the cadence client using a configured CadenceClientHelper instance
	client, err := clientHelper.Builder.BuildCadenceClient()
	if err != nil {
		buildReply(reply, cadenceerrors.NewCadenceError(err.Error()))

		return reply
	}

	// create the context
	ctx, cancel := context.WithTimeout(context.Background(), cadenceClientTimeout)
	defer cancel()

	// signalwithstart the specified workflow
	workflowExecution, err := client.SignalWithStartWorkflow(ctx,
		*request.GetWorkflowID(),
		*request.GetSignalName(),
		request.GetSignalArgs(),
		*request.GetOptions(),
		*request.GetWorkflow(),
		request.GetWorkflowArgs(),
	)

	if err != nil {
		buildReply(reply, cadenceerrors.NewCadenceError(err.Error()))

		return reply
	}

	// build the reply
	buildReply(reply, nil, workflowExecution)

	return reply
}

func handleWorkflowSetCacheSizeRequest(request *messages.WorkflowSetCacheSizeRequest) messages.IProxyReply {

	// $debug(jack.burns): DELETE THIS!
	logger.Debug("WorkflowSetCacheSizeRequest Received", zap.Int("ProccessId", os.Getpid()))

	// new WorkflowSetCacheSizeReply
	reply := createReplyMessage(request)

	// check to see if a connection has been made with the
	// cadence client
	if clientHelper == nil {
		buildReply(reply, cadenceerrors.NewCadenceError(errConnection.Error()))

		return reply
	}

	// set the sticky workflow cache size
	worker.SetStickyWorkflowCacheSize(request.GetSize())

	// build the reply
	buildReply(reply, nil)

	return reply
}

func handleWorkflowMutableRequest(request *messages.WorkflowMutableRequest) messages.IProxyReply {

	// $debug(jack.burns): DELETE THIS!
	logger.Debug("WorkflowMutableRequest Received", zap.Int("ProccessId", os.Getpid()))

	// new WorkflowMutableReply
	reply := createReplyMessage(request)

	// check to see if a connection has been made with the
	// cadence client
	if clientHelper == nil {
		buildReply(reply, cadenceerrors.NewCadenceError(errConnection.Error()))

		return reply
	}

	// get the contextID and the corresponding context
	wectx := cadenceworkflows.WorkflowContexts.Get(request.GetContextID())
	if wectx == nil {
		buildReply(reply, cadenceerrors.NewCadenceError(errEntityNotExist.Error()))

		return reply
	}

	// f function for workflow.MutableSideEffect
	mutableFunc := func(ctx workflow.Context) interface{} {
		return request.GetResult()
	}

	// the equals function for workflow.MutableSideEffect
	equals := func(a, b interface{}) bool {

		// check if the results are *cadencerrors.CadenceError
		if v, ok := a.(*cadenceerrors.CadenceError); ok {
			if _v, _ok := b.(*cadenceerrors.CadenceError); _ok {
				if v.GetType() == _v.GetType() &&
					v.ToString() == _v.ToString() {
					return true
				}
				return false
			}
			return false
		}

		// check if the results are []byte
		if v, ok := a.([]byte); ok {
			if _v, _ok := b.([]byte); _ok {
				return bytes.Equal(v, _v)
			}
			return false
		}
		return false
	}

	// TODO: JACK -- UPDATE TO CALL MutableSideEffect
	// or SideEffect
	// if request.GetUpdate() {

	// }

	// execute the cadence server SideEffectMutable call
	sideEffectValue := workflow.MutableSideEffect(wectx.GetContext(),
		*request.GetMutableID(),
		mutableFunc,
		equals,
	)

	// extract the result
	var result []byte
	if sideEffectValue.HasValue() {
		err := sideEffectValue.Get(&result)

		// check the error of retreiving the value
		if err != nil {
			buildReply(reply, cadenceerrors.NewCadenceError(err.Error()))

			return reply
		}

		// build reply
		buildReply(reply, nil, result)
	}

	return reply
}

func handleWorkflowDescribeExecutionRequest(request *messages.WorkflowDescribeExecutionRequest) messages.IProxyReply {

	// $debug(jack.burns): DELETE THIS!
	logger.Debug("WorkflowDescribeExecutionRequest Received", zap.Int("ProccessId", os.Getpid()))

	// new WorkflowDescribeExecutionReply
	reply := createReplyMessage(request)

	// check to see if a connection has been made with the
	// cadence client
	if clientHelper == nil {
		buildReply(reply, cadenceerrors.NewCadenceError(errConnection.Error()))

		return reply
	}

	// build the cadence client using a configured CadenceClientHelper instance
	client, err := clientHelper.Builder.BuildCadenceClient()
	if err != nil {
		buildReply(reply, cadenceerrors.NewCadenceError(err.Error()))

		return reply
	}

	// create the context to cancel the workflow
	ctx, cancel := context.WithTimeout(context.Background(), cadenceClientTimeout)
	defer cancel()

	// DescribeWorkflow call to cadence client
	describeWorkflowExecutionResponse, err := client.DescribeWorkflowExecution(ctx, *request.GetWorkflowID(), *request.GetRunID())
	if err != nil {
		buildReply(reply, cadenceerrors.NewCadenceError(err.Error()))

		return reply
	}

	// build reply
	buildReply(reply, nil, describeWorkflowExecutionResponse)

	return reply
}

func handleWorkflowGetResultRequest(request *messages.WorkflowGetResultRequest) messages.IProxyReply {

	// $debug(jack.burns): DELETE THIS!
	logger.Debug("WorkflowGetResultRequest Received", zap.Int("ProccessId", os.Getpid()))

	// new WorkflowGetResultReply
	reply := createReplyMessage(request)

	// check to see if a connection has been made with the
	// cadence client
	if clientHelper == nil {
		buildReply(reply, cadenceerrors.NewCadenceError(errConnection.Error()))

		return reply
	}

	// create the context to cancel the workflow
	ctx, cancel := context.WithTimeout(context.Background(), cadenceClientTimeout)
	defer cancel()

	// call GetWorkflow
	workflowRun, err := clientHelper.GetWorkflow(ctx,
		*request.GetWorkflowID(),
		*request.GetRunID(),
	)

	if err != nil {
		buildReply(reply, cadenceerrors.NewCadenceError(err.Error()))

		return reply
	}

	// get the result of WorkflowRun
	var result []byte
	err = workflowRun.Get(ctx, &result)
	if err != nil {
		buildReply(reply, cadenceerrors.NewCadenceError(err.Error()))

		return reply
	}

	// build the reply
	buildReply(reply, nil, result)

	return reply
}

func handleWorkflowSignalSubscribeRequest(request *messages.WorkflowSignalSubscribeRequest) messages.IProxyReply {

	// $debug(jack.burns): DELETE THIS!
	logger.Debug("WorkflowSignalSubscribeRequest Received", zap.Int("ProccessId", os.Getpid()))

	// new WorkflowSignalSubscribeReply
	reply := createReplyMessage(request)

	// check to see if a connection has been made with the
	// cadence client
	if clientHelper == nil {
		buildReply(reply, cadenceerrors.NewCadenceError(errConnection.Error()))

		return reply
	}

	// get the contextID and the corresponding context
	contextID := request.GetContextID()
	wectx := cadenceworkflows.WorkflowContexts.Get(contextID)
	if wectx == nil {
		buildReply(reply, cadenceerrors.NewCadenceError(errEntityNotExist.Error()))

		return reply
	}

	// placeholder to receive signal args from the
	// signal upon receive
	var signalArgs []byte
	signalName := request.GetSignalName()

	// create a selector, add a receiver and wait for the signal on
	// the channel
	ctx := wectx.GetContext()
	selector := workflow.NewSelector(ctx)
	selector = selector.AddReceive(workflow.GetSignalChannel(ctx, *signalName), func(channel workflow.Channel, more bool) {
		channel.Receive(ctx, &signalArgs)

		// $debug(jack.burns): DELETE THIS!
		logger.Debug("Received signal!", zap.String("signal", *signalName),
			zap.Binary("args", signalArgs))

		// create the WorkflowSignalInvokeRequest
		workflowSignalInvokeRequest := messages.NewWorkflowSignalInvokeRequest()
		workflowSignalInvokeRequest.SetSignalArgs(signalArgs)
		workflowSignalInvokeRequest.SetSignalName(signalName)
		workflowSignalInvokeRequest.SetContextID(contextID)

		// create the Operation for this request and add it to the operations map
		requestID := NextRequestID()
		future, settable := workflow.NewFuture(ctx)
		op := NewOperation(requestID, workflowSignalInvokeRequest)
		op.SetFuture(future)
		op.SetSettable(settable)
		op.SetContextID(contextID)
		Operations.Add(requestID, op)

		// send a request to the
		// Neon.Cadence Lib
		f := func(ctx workflow.Context) {

			// send the ActivityInvokeRequest
			resp, err := putToNeonCadenceClient(workflowSignalInvokeRequest)
			if err != nil {
				panic(err)
			}
			defer func() {

				// $debug(jack.burns): DELETE THIS!
				err := resp.Body.Close()
				if err != nil {
					logger.Error("could not close response body", zap.Error(err))
				}
			}()
		}

		// send the request
		workflow.Go(ctx, f)

		// wait for the future to be unblocked
		var result interface{}
		if err := future.Get(ctx, &result); err != nil {
			buildReply(reply, cadenceerrors.NewCadenceError(err.Error()))
		} else {
			buildReply(reply, nil)
		}
	})

	// wait on the channel
	selector.Select(ctx)

	return reply
}

func handleWorkflowSignalRequest(request *messages.WorkflowSignalRequest) messages.IProxyReply {

	// $debug(jack.burns): DELETE THIS!
	logger.Debug("WorkflowSignalRequest Received", zap.Int("ProccessId", os.Getpid()))

	// new WorkflowSignalReply
	reply := createReplyMessage(request)

	// check to see if a connection has been made with the
	// cadence client
	if clientHelper == nil {
		buildReply(reply, cadenceerrors.NewCadenceError(errConnection.Error()))

		return reply
	}

	// build the cadence client using a configured CadenceClientHelper instance
	client, err := clientHelper.Builder.BuildCadenceClient()
	if err != nil {
		buildReply(reply, cadenceerrors.NewCadenceError(err.Error()))

		return reply
	}

	// create the context to signal the workflow
	ctx, cancel := context.WithTimeout(context.Background(), cadenceClientTimeout)
	defer cancel()

	// signal the specified workflow
	err = client.SignalWorkflow(ctx,
		*request.GetWorkflowID(),
		*request.GetRunID(),
		*request.GetSignalName(),
		request.GetSignalArgs(),
	)

	if err != nil {
		buildReply(reply, cadenceerrors.NewCadenceError(err.Error()))

		return reply
	}

	// build the reply
	buildReply(reply, nil)

	return reply
}

func handleWorkflowHasLastResultRequest(request *messages.WorkflowHasLastResultRequest) messages.IProxyReply {

	// $debug(jack.burns): DELETE THIS!
	logger.Debug("WorkflowHasLastResultRequest Received", zap.Int("ProccessId", os.Getpid()))

	// new WorkflowHasLastResultReply
	reply := createReplyMessage(request)

	// check to see if a connection has been made with the
	// cadence client
	if clientHelper == nil {
		buildReply(reply, cadenceerrors.NewCadenceError(errConnection.Error()))

		return reply
	}

	// get the contextID and the corresponding context
	wectx := cadenceworkflows.WorkflowContexts.Get(request.GetContextID())
	if wectx == nil {
		buildReply(reply, cadenceerrors.NewCadenceError(errEntityNotExist.Error()))

		return reply
	}

	// build the reply
	buildReply(reply, nil, workflow.HasLastCompletionResult(wectx.GetContext()))

	return reply
}

func handleWorkflowGetLastResultRequest(request *messages.WorkflowGetLastResultRequest) messages.IProxyReply {

	// $debug(jack.burns): DELETE THIS!
	logger.Debug("WorkflowGetLastResultRequest Received", zap.Int("ProccessId", os.Getpid()))

	// new WorkflowGetLastResultReply
	reply := createReplyMessage(request)

	// check to see if a connection has been made with the
	// cadence client
	if clientHelper == nil {
		buildReply(reply, cadenceerrors.NewCadenceError(errConnection.Error()))

		return reply
	}

	// get the contextID and the corresponding context
	wectx := cadenceworkflows.WorkflowContexts.Get(request.GetContextID())
	if wectx == nil {
		buildReply(reply, cadenceerrors.NewCadenceError(errEntityNotExist.Error()))

		return reply
	}

	// get the last completion result from the cadence client
	var result []byte
	err := workflow.GetLastCompletionResult(wectx.GetContext(), &result)
	if err != nil {
		buildReply(reply, cadenceerrors.NewCadenceError(err.Error()))

		return reply
	}

	// build the reply
	buildReply(reply, nil, result)

	return reply
}

func handleWorkflowDisconnectContextRequest(request *messages.WorkflowDisconnectContextRequest) messages.IProxyReply {

	// $debug(jack.burns): DELETE THIS!
	logger.Debug("WorkflowDisconnectContextRequest Received", zap.Int("ProccessId", os.Getpid()))

	// new WorkflowDisconnectContextReply
	reply := createReplyMessage(request)

	// check to see if a connection has been made with the
	// cadence client
	if clientHelper == nil {
		buildReply(reply, cadenceerrors.NewCadenceError(errConnection.Error()))

		return reply
	}

	// get the contextID and the corresponding context
	wectx := cadenceworkflows.WorkflowContexts.Get(request.GetContextID())
	if wectx == nil {
		buildReply(reply, cadenceerrors.NewCadenceError(errEntityNotExist.Error()))

		return reply
	}

	// create a new disconnected context
	// and then replace the existing one with the new one
	disconnectedCtx, cancel := workflow.NewDisconnectedContext(wectx.GetContext())
	wectx.SetContext(disconnectedCtx)
	wectx.SetCancelFunction(cancel)

	// build the reply
	buildReply(reply, nil)

	return reply
}

func handleWorkflowGetTimeRequest(request *messages.WorkflowGetTimeRequest) messages.IProxyReply {

	// $debug(jack.burns): DELETE THIS!
	logger.Debug("WorkflowGetTimeRequest Received", zap.Int("ProccessId", os.Getpid()))

	// new WorkflowGetTimeReply
	reply := createReplyMessage(request)

	// check to see if a connection has been made with the
	// cadence client
	if clientHelper == nil {
		buildReply(reply, cadenceerrors.NewCadenceError(errConnection.Error()))

		return reply
	}

	// get the contextID and the corresponding context
	wectx := cadenceworkflows.WorkflowContexts.Get(request.GetContextID())
	if wectx == nil {
		buildReply(reply, cadenceerrors.NewCadenceError(errEntityNotExist.Error()))

		return reply
	}

	// build the reply
	buildReply(reply, nil, workflow.Now(wectx.GetContext()))

	return reply
}

func handleWorkflowSleepRequest(request *messages.WorkflowSleepRequest) messages.IProxyReply {

	// $debug(jack.burns): DELETE THIS!
	logger.Debug("WorkflowSleepRequest Received", zap.Int("ProccessId", os.Getpid()))

	// new WorkflowSleepReply
	reply := createReplyMessage(request)

	// check to see if a connection has been made with the
	// cadence client
	if clientHelper == nil {
		buildReply(reply, cadenceerrors.NewCadenceError(errConnection.Error()))

		return reply
	}

	// get the contextID and the corresponding context
	wectx := cadenceworkflows.WorkflowContexts.Get(request.GetContextID())
	if wectx == nil {
		buildReply(reply, cadenceerrors.NewCadenceError(errEntityNotExist.Error()))

		return reply
	}

	// pause the current workflow for the specified duration
	err := workflow.Sleep(wectx.GetContext(), request.GetDuration())
	if err != nil {
		buildReply(reply, cadenceerrors.NewCadenceError(err.Error()))

		return reply
	}

	// build the reply
	buildReply(reply, nil)

	return reply
}

func handleWorkflowExecuteChildRequest(request *messages.WorkflowExecuteChildRequest) messages.IProxyReply {

	// $debug(jack.burns): DELETE THIS!
	logger.Debug("WorkflowExecuteChildRequest Received", zap.Int("ProccessId", os.Getpid()))

	// new WorkflowExecuteChildReply
	reply := createReplyMessage(request)

	// check to see if a connection has been made with the
	// cadence client
	if clientHelper == nil {
		buildReply(reply, cadenceerrors.NewCadenceError(errConnection.Error()))

		return reply
	}

	// get the contextID and the corresponding context
	wectx := cadenceworkflows.WorkflowContexts.Get(request.GetContextID())
	if wectx == nil {
		buildReply(reply, cadenceerrors.NewCadenceError(errEntityNotExist.Error()))

		return reply
	}

	// set options on the context
	var opts workflow.ChildWorkflowOptions
	if v := request.GetOptions(); v != nil {
		opts = *v
	} else {
		opts = workflow.ChildWorkflowOptions{
			ExecutionStartToCloseTimeout: cadenceClientTimeout,
			TaskStartToCloseTimeout:      cadenceClientTimeout,
		}
	}

	// set cancellation on the context
	// execute the child workflow
	ctx := workflow.WithChildOptions(wectx.GetContext(), opts)
	ctx, cancel := workflow.WithCancel(ctx)
	childFuture := workflow.ExecuteChildWorkflow(ctx,
		*request.GetWorkflow(),
		request.GetArgs(),
	)

	// create the new ChildContext
	cctx := cadenceworkflows.NewChildContext()
	cctx.SetCancelFunction(cancel)
	cctx.SetFuture(childFuture)

	// add the ChildWorkflowFuture and the cancel func to the
	// ChildContexts map in the parent workflow's entry
	// in the WorkflowContexts map
	childID := wectx.AddChildContext(cadenceworkflows.NextChildID(), cctx)

	// build the reply
	buildReply(reply, nil, childID)

	return reply
}

func handleWorkflowWaitForChildRequest(request *messages.WorkflowWaitForChildRequest) messages.IProxyReply {

	// $debug(jack.burns): DELETE THIS!
	logger.Debug("WorkflowWaitForChildRequest Received", zap.Int("ProccessId", os.Getpid()))

	// new WorkflowWaitForChildReply
	reply := createReplyMessage(request)

	// check to see if a connection has been made with the
	// cadence client
	if clientHelper == nil {
		buildReply(reply, cadenceerrors.NewCadenceError(errConnection.Error()))

		return reply
	}

	// get the child context from the parent workflow context
	childID := request.GetChildID()
	wectx := cadenceworkflows.WorkflowContexts.Get(request.GetContextID())
	cctx := wectx.GetChildContext(childID)
	if cctx == nil {
		buildReply(reply, cadenceerrors.NewCadenceError(errEntityNotExist.Error()))

		return reply
	}

	// wait on the child workflow
	var result interface{}
	if err := cctx.GetFuture().GetChildWorkflowExecution().Get(wectx.GetContext(), result); err != nil {
		buildReply(reply, cadenceerrors.NewCadenceError(err.Error()))

		return reply
	}

	// remove the child context
	defer func() {
		_ = wectx.RemoveChildContext(childID)
	}()

	// build the reply
	buildReply(reply, nil, result)

	return reply
}

func handleWorkflowSignalChildRequest(request *messages.WorkflowSignalChildRequest) messages.IProxyReply {

	// $debug(jack.burns): DELETE THIS!
	logger.Debug("WorkflowSignalChildRequest Received", zap.Int("ProccessId", os.Getpid()))

	// new WorkflowSignalChildReply
	reply := createReplyMessage(request)

	// check to see if a connection has been made with the
	// cadence client
	if clientHelper == nil {
		buildReply(reply, cadenceerrors.NewCadenceError(errConnection.Error()))

		return reply
	}

	// get the child context from the parent workflow context
	wectx := cadenceworkflows.WorkflowContexts.Get(request.GetContextID())
	cctx := wectx.GetChildContext(request.GetChildID())
	if cctx == nil {
		buildReply(reply, cadenceerrors.NewCadenceError(errEntityNotExist.Error()))

		return reply
	}

	// signal the child workflow
	ctx := wectx.GetContext()
	future := cctx.GetFuture().SignalChildWorkflow(ctx,
		*request.GetSignalName(),
		request.GetSignalArgs(),
	)

	// wait on the future
	var result []byte
	if err := future.Get(ctx, result); err != nil {
		buildReply(reply, cadenceerrors.NewCadenceError(err.Error()))

		return reply
	}

	// build the reply
	buildReply(reply, nil, result)

	return reply
}

func handleWorkflowCancelChildRequest(request *messages.WorkflowCancelChildRequest) messages.IProxyReply {

	// $debug(jack.burns): DELETE THIS!
	logger.Debug("WorkflowCancelChildRequest Received", zap.Int("ProccessId", os.Getpid()))

	// new WorkflowCancelChildReply
	reply := createReplyMessage(request)

	// check to see if a connection has been made with the
	// cadence client
	if clientHelper == nil {
		buildReply(reply, cadenceerrors.NewCadenceError(errConnection.Error()))

		return reply
	}

	// get the child context from the parent workflow context
	childID := request.GetChildID()
	wectx := cadenceworkflows.WorkflowContexts.Get(request.GetContextID())
	cctx := wectx.GetChildContext(childID)
	if cctx == nil {
		buildReply(reply, cadenceerrors.NewCadenceError(errEntityNotExist.Error()))

		return reply
	}

	// call the cancel function
	go cctx.GetCancelFunction()

	// remove the child context
	defer func() {
		_ = wectx.RemoveChildContext(childID)
	}()

	// build the reply
	buildReply(reply, nil)

	return reply
}

func handleWorkflowSetQueryHandlerRequest(request *messages.WorkflowSetQueryHandlerRequest) messages.IProxyReply {

	// $debug(jack.burns): DELETE THIS!
	logger.Debug("WorkflowSetQueryHandlerRequest Received", zap.Int("ProccessId", os.Getpid()))

	// new WorkflowSetQueryHandlerReply
	reply := createReplyMessage(request)

	// check to see if a connection has been made with the
	// cadence client
	if clientHelper == nil {
		buildReply(reply, cadenceerrors.NewCadenceError(errConnection.Error()))

		return reply
	}

	// get the contextID and the corresponding context
	contextID := request.GetContextID()
	wectx := cadenceworkflows.WorkflowContexts.Get(contextID)
	if wectx == nil {
		buildReply(reply, cadenceerrors.NewCadenceError(errEntityNotExist.Error()))

		return reply
	}

	// get the context
	// get the SignalName
	// define the handler function
	ctx := wectx.GetContext()
	queryName := request.GetQueryName()
	queryHandler := func(queryArgs []byte) ([]byte, error) {

		// create the WorkflowSignalInvokeRequest
		workflowQueryInvokeRequest := messages.NewWorkflowQueryInvokeRequest()
		workflowQueryInvokeRequest.SetQueryArgs(queryArgs)
		workflowQueryInvokeRequest.SetQueryName(queryName)
		workflowQueryInvokeRequest.SetContextID(contextID)

		// create the Operation for this request and add it to the operations map
		requestID := NextRequestID()
		future, settable := workflow.NewFuture(ctx)
		op := NewOperation(requestID, workflowQueryInvokeRequest)
		op.SetFuture(future)
		op.SetSettable(settable)
		op.SetContextID(contextID)
		Operations.Add(requestID, op)

		// send a request to the
		// Neon.Cadence Lib
		f := func(ctx workflow.Context) {

			// send the ActivityInvokeRequest
			resp, err := putToNeonCadenceClient(workflowQueryInvokeRequest)
			if err != nil {
				panic(err)
			}
			defer func() {

				// $debug(jack.burns): DELETE THIS!
				err := resp.Body.Close()
				if err != nil {
					logger.Error("could not close response body", zap.Error(err))
				}
			}()
		}

		// send the request
		workflow.Go(ctx, f)

		// wait for the future to be unblocked
		var result []byte
		if err := future.Get(ctx, &result); err != nil {
			return nil, err
		}

		return result, nil
	}

	// Set the query handler with the
	// cadence server
	err := workflow.SetQueryHandler(ctx, *queryName, queryHandler)
	if err != nil {
		buildReply(reply, cadenceerrors.NewCadenceError(err.Error()))

		return reply
	}

	// build the reply
	buildReply(reply, nil)

	return reply
}

func handleWorkflowQueryRequest(request *messages.WorkflowQueryRequest) messages.IProxyReply {

	// $debug(jack.burns): DELETE THIS!
	logger.Debug("WorkflowQueryRequest Received", zap.Int("ProccessId", os.Getpid()))

	// new WorkflowQueryReply
	reply := createReplyMessage(request)

	// check to see if a connection has been made with the
	// cadence client
	if clientHelper == nil {
		buildReply(reply, cadenceerrors.NewCadenceError(errConnection.Error()))

		return reply
	}

	// build the cadence client using a configured CadenceClientHelper instance
	client, err := clientHelper.Builder.BuildCadenceClient()
	if err != nil {
		buildReply(reply, cadenceerrors.NewCadenceError(err.Error()))

		return reply
	}

	// create the context
	ctx, cancel := context.WithTimeout(context.Background(), cadenceClientTimeout)
	defer cancel()

	// query the workflow via the cadence client
	value, err := client.QueryWorkflow(ctx,
		*request.GetWorkflowID(),
		*request.GetRunID(),
		*request.GetQueryName(),
		request.GetQueryArgs(),
	)

	if err != nil {
		buildReply(reply, cadenceerrors.NewCadenceError(err.Error()))

		return reply
	}

	// extract the result
	var result []byte
	if value.HasValue() {
		err = value.Get(&result)
		if err != nil {
			buildReply(reply, cadenceerrors.NewCadenceError(err.Error()))

			return reply
		}
	}

	// build reply
	buildReply(reply, nil, result)

	return reply
}

// -------------------------------------------------------------------------
// IProxyRequest activity message type handler methods

func handleActivityRegisterRequest(request *messages.ActivityRegisterRequest) messages.IProxyReply {

	// $debug(jack.burns): DELETE THIS!
	logger.Debug("ActivityRegisterRequest Received", zap.Int("ProccessId", os.Getpid()))

	// new ActivityRegisterReply
	reply := createReplyMessage(request)

	// check to see if a connection has been made with the
	// cadence client
	if clientHelper == nil {
		buildReply(reply, cadenceerrors.NewCadenceError(errConnection.Error()))

		return reply
	}

	// define the activity function
	contextID := cadenceactivities.NextContextID()
	activityName := request.GetName()
	activityFunc := func(ctx context.Context, input []byte) ([]byte, error) {

		// create an activity context entry in the ActivityContexts map
		actx := cadenceactivities.NewActivityContext(ctx)
		actx.SetActivityName(activityName)

		// add the context to ActivityContexts
		contextID = cadenceactivities.ActivityContexts.Add(contextID, actx)

		// Send a ActivityInvokeRequest to the Neon.Cadence Lib
		// cadence-client
		requestID := NextRequestID()
		activityInvokeRequest := messages.NewActivityInvokeRequest()
		activityInvokeRequest.SetRequestID(requestID)
		activityInvokeRequest.SetArgs(input)
		activityInvokeRequest.SetContextID(contextID)
		activityInvokeRequest.SetActivity(request.GetName())

		// create the Operation for this request and add it to the operations map
		invokeReplyChannel := make(chan interface{})
		op := NewOperation(requestID, activityInvokeRequest)
		op.SetChannel(invokeReplyChannel)
		op.SetContextID(contextID)
		Operations.Add(requestID, op)

		// get worker stop channel on the context
		stopChan := activity.GetWorkerStopChannel(ctx)

		// send a request to the
		// Neon.Cadence Lib
		f := func(message messages.IProxyRequest) {

			// send the ActivityInvokeRequest
			resp, err := putToNeonCadenceClient(message)
			if err != nil {
				panic(err)
			}
			defer func() {

				// $debug(jack.burns): DELETE THIS!
				err := resp.Body.Close()
				if err != nil {
					logger.Error("could not close response body", zap.Error(err))
				}
			}()
		}

		// Send and wait for
		// ActivityStoppingRequest
		s := func() {

			// wait on the channel to receive the stop signal
			<-stopChan

			// send an ActivityStoppingRequest to the client
			requestID := NextRequestID()
			activityStoppingRequest := messages.NewActivityStoppingRequest()
			activityStoppingRequest.SetRequestID(requestID)
			activityStoppingRequest.SetActivityID(request.GetName())
			activityStoppingRequest.SetContextID(contextID)

			// create the Operation for this request and add it to the operations map
			stoppingReplyChan := make(chan interface{})
			op := NewOperation(requestID, activityStoppingRequest)
			op.SetChannel(stoppingReplyChan)
			op.SetContextID(contextID)
			Operations.Add(requestID, op)

			// send the request and wait for the reply
			go f(activityStoppingRequest)
			<-stoppingReplyChan
		}

		// run go routines
		go s()
		go f(activityInvokeRequest)

		// wait for ActivityInvokeReply
		result := <-op.GetChannel()
		switch s := result.(type) {
		case error:
			return nil, s
		case []byte:
			return s, nil
		default:
			return nil, fmt.Errorf("unexpected result type %v.  result must be an error or []byte", reflect.TypeOf(s))
		}
	}

	// register the activity
	activity.RegisterWithOptions(activityFunc, activity.RegisterOptions{Name: *activityName})

	// $debug(jack.burns): DELETE THIS!
	logger.Debug("Activity Successfully Registered", zap.String("ActivityName", *activityName))
	buildReply(reply, nil)

	return reply
}

func handleActivityExecuteRequest(request *messages.ActivityExecuteRequest) messages.IProxyReply {

	// $debug(jack.burns): DELETE THIS!
	logger.Debug("ActivityExecuteRequest Received", zap.Int("ProccessId", os.Getpid()))

	// new ActivityExecuteReply
	reply := createReplyMessage(request)

	// check to see if a connection has been made with the
	// cadence client
	if clientHelper == nil {
		buildReply(reply, cadenceerrors.NewCadenceError(errConnection.Error()))

		return reply
	}

	// get the contextID and the corresponding context
	contextID := request.GetContextID()
	wectx := cadenceworkflows.WorkflowContexts.Get(contextID)
	if wectx == nil {
		buildReply(reply, cadenceerrors.NewCadenceError(errEntityNotExist.Error()))

		return reply
	}

	// get the activity options, the context,
	// and set the activity options on the context
	opts := request.GetOptions()
	ctx := workflow.WithActivityOptions(wectx.GetContext(), *opts)

	// execute the activity
	var result []byte
	if err := workflow.ExecuteActivity(ctx, *request.GetActivity(), request.GetArgs()).Get(ctx, &result); err != nil {
		buildReply(reply, cadenceerrors.NewCadenceError(err.Error()))

		return reply
	}

	// build the reply
	buildReply(reply, nil, result)

	return reply
}

func handleActivityHasHeartbeatDetailsRequest(request *messages.ActivityHasHeartbeatDetailsRequest) messages.IProxyReply {

	// $debug(jack.burns): DELETE THIS!
	logger.Debug("ActivityHasHeartbeatDetailsRequest Received", zap.Int("ProccessId", os.Getpid()))

	// new ActivityHasHeartbeatDetailsReply
	reply := createReplyMessage(request)

	// check to see if a connection has been made with the
	// cadence client
	if clientHelper == nil {
		buildReply(reply, cadenceerrors.NewCadenceError(errConnection.Error()))

		return reply
	}

	// create the new context and a []byte to
	// drop the heartbeat details into
	ctx, cancel := context.WithTimeout(context.Background(), cadenceClientTimeout)
	defer cancel()

	// build the reply
	buildReply(reply, nil, activity.HasHeartbeatDetails(ctx))

	return reply
}

func handleActivityGetHeartbeatDetailsRequest(request *messages.ActivityGetHeartbeatDetailsRequest) messages.IProxyReply {

	// $debug(jack.burns): DELETE THIS!
	logger.Debug("ActivityGetHeartbeatDetailsRequest Received", zap.Int("ProccessId", os.Getpid()))

	// new ActivityGetHeartbeatDetailsReply
	reply := createReplyMessage(request)

	// check to see if a connection has been made with the
	// cadence client
	if clientHelper == nil {
		buildReply(reply, cadenceerrors.NewCadenceError(errConnection.Error()))

		return reply
	}

	// create the new context and a []byte to
	// drop the heartbeat details into
	var details []byte
	ctx, cancel := context.WithTimeout(context.Background(), cadenceClientTimeout)
	defer cancel()

	// get the activity heartbeat details
	err := activity.GetHeartbeatDetails(ctx, &details)
	if err != nil {
		buildReply(reply, cadenceerrors.NewCadenceError(err.Error()))

		return reply
	}

	// build the reply
	buildReply(reply, nil, details)

	return reply
}

func handleActivityRecordHeartbeatRequest(request *messages.ActivityRecordHeartbeatRequest) messages.IProxyReply {

	// $debug(jack.burns): DELETE THIS!
	logger.Debug("ActivityRecordHeartbeatRequest Received", zap.Int("ProccessId", os.Getpid()))

	// new ActivityRecordHeartbeatReply
	reply := createReplyMessage(request)

	// check to see if a connection has been made with the
	// cadence client
	if clientHelper == nil {
		buildReply(reply, cadenceerrors.NewCadenceError(errConnection.Error()))

		return reply
	}

	// create the new context and a []byte to
	// drop the heartbeat details into
	var details []byte
	ctx, cancel := context.WithTimeout(context.Background(), cadenceClientTimeout)
	defer cancel()

	// record the heartbeat details
	activity.RecordHeartbeat(ctx, details)

	// build the reply
	buildReply(reply, nil, details)

	return reply
}

func handleActivityGetInfoRequest(request *messages.ActivityGetInfoRequest) messages.IProxyReply {

	// $debug(jack.burns): DELETE THIS!
	logger.Debug("ActivityGetInfoRequest Received", zap.Int("ProccessId", os.Getpid()))

	// new ActivityGetInfoReply
	reply := createReplyMessage(request)

	// check to see if a connection has been made with the
	// cadence client
	if clientHelper == nil {
		buildReply(reply, cadenceerrors.NewCadenceError(errConnection.Error()))

		return reply
	}

	// create the context
	ctx, cancel := context.WithTimeout(context.Background(), cadenceClientTimeout)
	defer cancel()

	// build the reply
	buildReply(reply, nil, activity.GetInfo(ctx))

	return reply
}

func handleActivityCompleteRequest(request *messages.ActivityCompleteRequest) messages.IProxyReply {

	// $debug(jack.burns): DELETE THIS!
	logger.Debug("ActivityCompleteRequest Received", zap.Int("ProccessId", os.Getpid()))

	// new ActivityCompleteReply
	reply := createReplyMessage(request)

	// check to see if a connection has been made with the
	// cadence client
	if clientHelper == nil {
		buildReply(reply, cadenceerrors.NewCadenceError(errConnection.Error()))

		return reply
	}

	// build the cadence client using a configured CadenceClientHelper instance
	client, err := clientHelper.Builder.BuildCadenceClient()
	if err != nil {
		buildReply(reply, cadenceerrors.NewCadenceError(err.Error()))

		return reply
	}

	// create the context
	ctx, cancel := context.WithTimeout(context.Background(), cadenceClientTimeout)
	defer cancel()

	// complete the activity
	err = client.CompleteActivity(ctx,
		request.GetTaskToken(),
		request.GetResult(),
		errors.New(request.GetError().ToString()),
	)

	if err != nil {
		buildReply(reply, cadenceerrors.NewCadenceError(err.Error()))

		return reply
	}

	// build the reply
	buildReply(reply, nil)

	return reply
}

func handleActivityExecuteLocalRequest(request *messages.ActivityExecuteLocalRequest) messages.IProxyReply {

	// $debug(jack.burns): DELETE THIS!
	logger.Debug("ActivityExecuteLocalRequest Received", zap.Int("ProccessId", os.Getpid()))

	// new ActivityExecuteLocalReply
	reply := createReplyMessage(request)

	// check to see if a connection has been made with the
	// cadence client
	if clientHelper == nil {
		buildReply(reply, cadenceerrors.NewCadenceError(errConnection.Error()))

		return reply
	}

	contextID := request.GetContextID()
	wectx := cadenceworkflows.WorkflowContexts.Get(contextID)
	if wectx == nil {
		buildReply(reply, cadenceerrors.NewCadenceError(errEntityNotExist.Error()))

		return reply
	}

	// the local activity function
	activityTypeID := request.GetActivityTypeID()
	args := request.GetArgs()
	localActivityFunc := func(ctx context.Context, input []byte) ([]byte, error) {

		// create an activity context entry in the ActivityContexts map
		actx := cadenceactivities.NewActivityContext(ctx)

		// add the context to ActivityContexts
		activityContextID := cadenceactivities.ActivityContexts.Add(cadenceactivities.NextContextID(), actx)

		// Send a ActivityInvokeLocalRequest to the Neon.Cadence Lib
		// cadence-client
		requestID := NextRequestID()
		activityInvokeLocalRequest := messages.NewActivityInvokeLocalRequest()
		activityInvokeLocalRequest.SetRequestID(requestID)
		activityInvokeLocalRequest.SetContextID(contextID)
		activityInvokeLocalRequest.SetArgs(input)
		activityInvokeLocalRequest.SetActivityTypeID(activityTypeID)
		activityInvokeLocalRequest.SetActivityContextID(activityContextID)

		// create the Operation for this request and add it to the operations map
		op := NewOperation(requestID, activityInvokeLocalRequest)
		op.SetChannel(make(chan interface{}))
		op.SetContextID(activityContextID)
		Operations.Add(requestID, op)

		// send a request to the
		// Neon.Cadence Lib
		f := func(message messages.IProxyRequest) {

			// send the ActivityInvokeRequest
			resp, err := putToNeonCadenceClient(message)
			if err != nil {
				panic(err)
			}
			defer func() {

				// $debug(jack.burns): DELETE THIS!
				err := resp.Body.Close()
				if err != nil {
					logger.Error("could not close response body", zap.Error(err))
				}
			}()
		}

		// send the request
		go f(activityInvokeLocalRequest)

		// wait for ActivityInvokeReply
		result := <-op.GetChannel()
		switch s := result.(type) {
		case error:
			return nil, s
		case []byte:
			return s, nil
		default:
			return nil, fmt.Errorf("unexpected result type %v.  result must be an error or []byte", reflect.TypeOf(s))
		}
	}

	// get the activity options
	var opts workflow.LocalActivityOptions
	if v := request.GetOptions(); v == nil {
		opts = *v
		if opts.ScheduleToCloseTimeout <= 0 {
			opts.ScheduleToCloseTimeout = cadenceClientTimeout
		}
	} else {
		opts = workflow.LocalActivityOptions{
			ScheduleToCloseTimeout: cadenceClientTimeout,
		}
	}

	// and set the activity options on the context
	ctx := workflow.WithLocalActivityOptions(wectx.GetContext(), opts)

	// wait for the future to be unblocked
	var result []byte
	if err := workflow.ExecuteLocalActivity(ctx, localActivityFunc, args).Get(ctx, &result); err != nil {
		buildReply(reply, cadenceerrors.NewCadenceError(err.Error()))
	} else {
		buildReply(reply, nil, result)
	}

	return reply
}

// -------------------------------------------------------------------------
// Helpers for sending ProxyReply messages back to Neon.Cadence Library

func putToNeonCadenceClient(message messages.IProxyMessage) (*http.Response, error) {

	// serialize the message
	proxyMessage := message.GetProxyMessage()
	content, err := proxyMessage.Serialize(false)
	if err != nil {

		// $debug(jack.burns): DELETE THIS!
		logger.Debug("Error serializing proxy message", zap.Error(err))
		return nil, err
	}

	// create a buffer with the serialized bytes to reply with
	// and create the PUT request
	buf := bytes.NewBuffer(content)
	req, err := http.NewRequest(http.MethodPut, replyAddress, buf)
	if err != nil {

		// $debug(jack.burns): DELETE THIS!
		logger.Debug("Error creating Neon.Cadence Library request", zap.Error(err))
		return nil, err
	}

	// set the request header to specified content type
	// and disable http request compression
	req.Header.Set("Content-Type", ContentType)
	req.Header.Set("Accept-Encoding", "identity")

	// $debug(jack.burns): DELETE THIS!
	logger.Debug("Neon.Cadence Library request",
		zap.String("Request Address", req.URL.String()),
		zap.String("Request Content-Type", req.Header.Get("Content-Type")),
		zap.String("Request Method", req.Method),
	)

	// initialize the http.Client and send the request
	client := &http.Client{}
	resp, err := client.Do(req)
	if err != nil {

		// $debug(jack.burns): DELETE THIS!
		logger.Debug("Error sending Neon.Cadence Library request", zap.Error(err))
		return nil, err
	}

	return resp, nil
}
