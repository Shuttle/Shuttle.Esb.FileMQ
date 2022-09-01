using NUnit.Framework;
using Shuttle.Esb.Tests;

namespace Shuttle.Esb.FileMQ.Tests
{
	public class FileDeferredMessageTest : DeferredFixture
	{
		[Test]
		[TestCase(false)]
		[TestCase(true)]
		public void Should_be_able_to_perform_full_processing(bool isTransactionalEndpoint)
		{
			TestDeferredProcessing(FileQueueFixture.GetServiceCollection(), "filemq://local/{0}", isTransactionalEndpoint);
		}
	}
}