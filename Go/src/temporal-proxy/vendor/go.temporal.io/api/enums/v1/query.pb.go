// The MIT License
//
// Copyright (c) 2020 Temporal Technologies Inc.  All rights reserved.
//
// Permission is hereby granted, free of charge, to any person obtaining a copy
// of this software and associated documentation files (the "Software"), to deal
// in the Software without restriction, including without limitation the rights
// to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
// copies of the Software, and to permit persons to whom the Software is
// furnished to do so, subject to the following conditions:
//
// The above copyright notice and this permission notice shall be included in
// all copies or substantial portions of the Software.
//
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
// IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
// FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
// AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
// LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
// OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN
// THE SOFTWARE.

// Code generated by protoc-gen-gogo. DO NOT EDIT.
// source: temporal/api/enums/v1/query.proto

package enums

import (
	fmt "fmt"
	math "math"
	strconv "strconv"

	proto "github.com/gogo/protobuf/proto"
)

// Reference imports to suppress errors if they are not otherwise used.
var _ = proto.Marshal
var _ = fmt.Errorf
var _ = math.Inf

// This is a compile-time assertion to ensure that this generated file
// is compatible with the proto package it is being compiled against.
// A compilation error at this line likely means your copy of the
// proto package needs to be updated.
const _ = proto.GoGoProtoPackageIsVersion3 // please upgrade the proto package

type QueryResultType int32

const (
	QUERY_RESULT_TYPE_UNSPECIFIED QueryResultType = 0
	QUERY_RESULT_TYPE_ANSWERED    QueryResultType = 1
	QUERY_RESULT_TYPE_FAILED      QueryResultType = 2
)

var QueryResultType_name = map[int32]string{
	0: "Unspecified",
	1: "Answered",
	2: "Failed",
}

var QueryResultType_value = map[string]int32{
	"Unspecified": 0,
	"Answered":    1,
	"Failed":      2,
}

func (QueryResultType) EnumDescriptor() ([]byte, []int) {
	return fileDescriptor_b9a616a97224ce1d, []int{0}
}

type QueryRejectCondition int32

const (
	QUERY_REJECT_CONDITION_UNSPECIFIED QueryRejectCondition = 0
	// None indicates that query should not be rejected.
	QUERY_REJECT_CONDITION_NONE QueryRejectCondition = 1
	// NotOpen indicates that query should be rejected if workflow is not open.
	QUERY_REJECT_CONDITION_NOT_OPEN QueryRejectCondition = 2
	// NotCompletedCleanly indicates that query should be rejected if workflow did not complete cleanly.
	QUERY_REJECT_CONDITION_NOT_COMPLETED_CLEANLY QueryRejectCondition = 3
)

var QueryRejectCondition_name = map[int32]string{
	0: "Unspecified",
	1: "None",
	2: "NotOpen",
	3: "NotCompletedCleanly",
}

var QueryRejectCondition_value = map[string]int32{
	"Unspecified":         0,
	"None":                1,
	"NotOpen":             2,
	"NotCompletedCleanly": 3,
}

func (QueryRejectCondition) EnumDescriptor() ([]byte, []int) {
	return fileDescriptor_b9a616a97224ce1d, []int{1}
}

func init() {
	proto.RegisterEnum("temporal.api.enums.v1.QueryResultType", QueryResultType_name, QueryResultType_value)
	proto.RegisterEnum("temporal.api.enums.v1.QueryRejectCondition", QueryRejectCondition_name, QueryRejectCondition_value)
}

func init() { proto.RegisterFile("temporal/api/enums/v1/query.proto", fileDescriptor_b9a616a97224ce1d) }

var fileDescriptor_b9a616a97224ce1d = []byte{
	// 361 bytes of a gzipped FileDescriptorProto
	0x1f, 0x8b, 0x08, 0x00, 0x00, 0x00, 0x00, 0x00, 0x02, 0xff, 0x7c, 0xd1, 0xcf, 0x4a, 0xeb, 0x40,
	0x18, 0x05, 0xf0, 0x4c, 0x2f, 0xdc, 0xc5, 0x6c, 0x6e, 0x08, 0x57, 0x28, 0xfe, 0xf9, 0x4a, 0x15,
	0x5c, 0x14, 0x49, 0x2c, 0xee, 0xe2, 0x2a, 0x4d, 0xbe, 0x42, 0x24, 0x4e, 0xd2, 0x74, 0xaa, 0xd4,
	0x4d, 0xa8, 0x1a, 0x24, 0xd2, 0x36, 0x31, 0x4d, 0x0b, 0xdd, 0xf9, 0x08, 0xae, 0x7d, 0x02, 0x9f,
	0xc0, 0x67, 0x70, 0xd9, 0x65, 0x97, 0x36, 0xdd, 0x88, 0xab, 0x3e, 0x82, 0x34, 0xb4, 0xa0, 0xd6,
	0xba, 0x1b, 0x66, 0x7e, 0xc3, 0x81, 0x73, 0x68, 0x31, 0xf1, 0x3b, 0x51, 0x18, 0xb7, 0xda, 0x4a,
	0x2b, 0x0a, 0x14, 0xbf, 0xdb, 0xef, 0xf4, 0x94, 0x41, 0x59, 0xb9, 0xeb, 0xfb, 0xf1, 0x50, 0x8e,
	0xe2, 0x30, 0x09, 0xa5, 0x8d, 0x25, 0x91, 0x5b, 0x51, 0x20, 0x67, 0x44, 0x1e, 0x94, 0x4b, 0x31,
	0xfd, 0x57, 0x9b, 0x2b, 0xd7, 0xef, 0xf5, 0xdb, 0x09, 0x1f, 0x46, 0xbe, 0x54, 0xa4, 0x3b, 0xb5,
	0x06, 0xba, 0x4d, 0xcf, 0xc5, 0x7a, 0xc3, 0xe2, 0x1e, 0x6f, 0x3a, 0xe8, 0x35, 0x58, 0xdd, 0x41,
	0xdd, 0xac, 0x9a, 0x68, 0x88, 0x82, 0x04, 0x74, 0x73, 0x95, 0x68, 0xac, 0x7e, 0x8e, 0x2e, 0x1a,
	0x22, 0x91, 0xb6, 0x69, 0x7e, 0xf5, 0xbd, 0xaa, 0x99, 0x16, 0x1a, 0x62, 0xae, 0xf4, 0x4c, 0xe8,
	0xff, 0x45, 0xe8, 0xad, 0x7f, 0x95, 0xe8, 0x61, 0xf7, 0x3a, 0x48, 0x82, 0xb0, 0x2b, 0xed, 0xd3,
	0xdd, 0xe5, 0xb7, 0x13, 0xd4, 0xb9, 0xa7, 0xdb, 0xcc, 0x30, 0xb9, 0x69, 0xb3, 0x6f, 0xf1, 0x05,
	0xba, 0xb5, 0xc6, 0x31, 0x9b, 0xa1, 0x48, 0xa4, 0x3d, 0x5a, 0x58, 0x0b, 0xb8, 0x67, 0x3b, 0xc8,
	0xc4, 0x9c, 0x74, 0x48, 0x0f, 0x7e, 0x41, 0xba, 0x7d, 0xea, 0x58, 0xc8, 0xd1, 0xf0, 0x74, 0x0b,
	0x35, 0x66, 0x35, 0xc5, 0x3f, 0x95, 0x47, 0x32, 0x9a, 0x80, 0x30, 0x9e, 0x80, 0x30, 0x9b, 0x00,
	0xb9, 0x4f, 0x81, 0x3c, 0xa5, 0x40, 0x5e, 0x52, 0x20, 0xa3, 0x14, 0xc8, 0x6b, 0x0a, 0xe4, 0x2d,
	0x05, 0x61, 0x96, 0x02, 0x79, 0x98, 0x82, 0x30, 0x9a, 0x82, 0x30, 0x9e, 0x82, 0x40, 0xf3, 0x41,
	0x28, 0xff, 0xd8, 0x7e, 0x85, 0x66, 0x35, 0x38, 0xf3, 0x81, 0x1c, 0x72, 0x51, 0xbc, 0xf9, 0xe4,
	0x82, 0xf0, 0xcb, 0x96, 0xc7, 0xd9, 0xe1, 0x3d, 0x97, 0xe7, 0x0b, 0xa0, 0xaa, 0x5a, 0x14, 0xa8,
	0x2a, 0xce, 0xaf, 0x55, 0xf5, 0xac, 0x7c, 0xf9, 0x37, 0xdb, 0xf9, 0xe8, 0x23, 0x00, 0x00, 0xff,
	0xff, 0xa3, 0x54, 0xf5, 0x2b, 0x0c, 0x02, 0x00, 0x00,
}

func (x QueryResultType) String() string {
	s, ok := QueryResultType_name[int32(x)]
	if ok {
		return s
	}
	return strconv.Itoa(int(x))
}
func (x QueryRejectCondition) String() string {
	s, ok := QueryRejectCondition_name[int32(x)]
	if ok {
		return s
	}
	return strconv.Itoa(int(x))
}
