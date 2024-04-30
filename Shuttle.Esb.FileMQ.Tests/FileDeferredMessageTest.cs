using System.Threading.Tasks;
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
			TestDeferredProcessing(FileQueueConfiguration.GetServiceCollection(), "filemq://local/{0}", isTransactionalEndpoint);
		}

		[Test]
		[TestCase(false)]
		[TestCase(true)]
		public async Task Should_be_able_to_perform_full_processing_async(bool isTransactionalEndpoint)
		{
			await TestDeferredProcessingAsync(FileQueueConfiguration.GetServiceCollection(), "filemq://local/{0}", isTransactionalEndpoint);
		}
	}
}