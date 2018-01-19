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

        public bool CanCreate(Uri uri)
        {
            Guard.AgainstNull(uri, "uri");

            var result = Scheme.Equals(uri.Scheme, StringComparison.InvariantCultureIgnoreCase);

            Guard.Against<NotSupportedException>(result && !string.IsNullOrEmpty(uri.Host) && !uri.Host.Equals("."),
                string.Format(Resources.HostNotPermittedException, uri.Host));

            return result;
        }
    }
}