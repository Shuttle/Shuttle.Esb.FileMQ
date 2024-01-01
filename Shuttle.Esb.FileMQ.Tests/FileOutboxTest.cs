using System.Threading.Tasks;
using NUnit.Framework;
using Shuttle.Esb.Tests;

namespace Shuttle.Esb.FileMQ.Tests
{
	public class FileOutboxTest : OutboxFixture
	{
		[Test]
		[TestCase(false)]
		[TestCase(true)]
		public void Should_be_able_handle_errors(bool isTransactionalEndpoint)
		{
			TestOutboxSending(FileQueueConfiguration.GetServiceCollection(), "filemq://local/{0}", 1, isTransactionalEndpoint);
		}

		[Test]
		[TestCase(false)]
		[TestCase(true)]
		public async Task Should_be_able_handle_errors_async(bool isTransactionalEndpoint)
		{
			await TestOutboxSendingAsync(FileQueueConfiguration.GetServiceCollection(), "filemq://local/{0}", 1, isTransactionalEndpoint);
		}
	}
}