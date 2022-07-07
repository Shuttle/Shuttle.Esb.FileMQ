﻿using NUnit.Framework;
using Shuttle.Esb.Tests;

namespace Shuttle.Esb.FileMQ.Tests
{
	public class FileInboxTest : InboxFixture
	{
		[Test]
		[TestCase(false)]
		[TestCase(true)]
		public void Should_be_able_handle_errors(bool isTransactionalEndpoint)
		{
			TestInboxError(FileMQFixture.GetServiceCollection(), FileMQExtensions.FileUri(), isTransactionalEndpoint);
		}

		[Test]
		[TestCase(100, false)]
		[TestCase(100, true)]
		public void Should_be_able_to_process_messages_concurrently(int msToComplete, bool isTransactionalEndpoint)
		{
			TestInboxConcurrency(FileMQFixture.GetServiceCollection(), FileMQExtensions.FileUri(), msToComplete, isTransactionalEndpoint);
		}

		[Test]
		[TestCase(100, false)]
		[TestCase(100, true)]
		public void Should_be_able_to_process_queue_timeously(int count, bool isTransactionalEndpoint)
		{
			TestInboxThroughput(FileMQFixture.GetServiceCollection(), FileMQExtensions.FileUri(), 1000, count, isTransactionalEndpoint);
		}

		[Test]
		public void Should_be_able_to_handle_a_deferred_message()
		{
			TestInboxDeferred(FileMQFixture.GetServiceCollection(), FileMQExtensions.FileUri());
		}
	}
}