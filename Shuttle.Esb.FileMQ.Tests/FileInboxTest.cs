using System.Threading.Tasks;
using NUnit.Framework;
using Shuttle.Esb.Tests;

namespace Shuttle.Esb.FileMQ.Tests
{
	public class FileInboxTest : InboxFixture
	{
		[TestCase(true, true)]
		[TestCase(true, false)]
		[TestCase(false, true)]
		[TestCase(false, false)]
		public async Task Should_be_able_handle_errors(bool hasErrorQueue, bool isTransactionalEndpoint)
		{
			await TestInboxError(FileQueueFixture.GetServiceCollection(), "filemq://local/{0}", hasErrorQueue, isTransactionalEndpoint);
		}

		[Test]
		[TestCase(100, false)]
		[TestCase(100, true)]
		public async Task Should_be_able_to_process_messages_concurrently(int msToComplete, bool isTransactionalEndpoint)
		{
			await TestInboxConcurrency(FileQueueFixture.GetServiceCollection(), "filemq://local/{0}", msToComplete, isTransactionalEndpoint);
		}

		[Test]
		[TestCase(100, false)]
		[TestCase(100, true)]
		public async Task Should_be_able_to_process_queue_timeously(int count, bool isTransactionalEndpoint)
		{
			await TestInboxThroughput(FileQueueFixture.GetServiceCollection(), "filemq://local/{0}", 1000, count, 3, isTransactionalEndpoint);
		}

		[Test]
		public async Task Should_be_able_to_handle_a_deferred_message()
		{
			await TestInboxDeferred(FileQueueFixture.GetServiceCollection(), "filemq://local/{0}");
		}
	}
}