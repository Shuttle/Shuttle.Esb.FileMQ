using System;
using Shuttle.Core.Contract;

namespace Shuttle.Esb.FileMQ
{
    public class FileQueueFactory : IQueueFactory
    {
        public string Scheme => "filemq";

        public IQueue Create(Uri uri)
        {
            Guard.AgainstNull(uri, "uri");

            return new FileQueue(uri);
        }
    }
}