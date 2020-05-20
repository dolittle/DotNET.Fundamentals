// Copyright (c) Dolittle. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Google.Protobuf;
using Google.Protobuf.Reflection;

namespace Dolittle.Services.Clients.for_ReverseCallClient
{
    public class MyClientMessage : IMessage
    {
        public MyResponse Response { get; set; }

        public MyConnectArguments Arguments { get; set; }

        public MessageDescriptor Descriptor => throw new System.NotImplementedException();

        public int CalculateSize()
        {
            return 0;
        }

        public void MergeFrom(CodedInputStream input)
        {
        }

        public void WriteTo(CodedOutputStream output)
        {
        }
    }
}