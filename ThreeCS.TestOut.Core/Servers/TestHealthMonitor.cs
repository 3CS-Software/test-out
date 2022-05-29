using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Timers;
using ThreeCS.TestOut.Core.Agents.Messages;
using ThreeCS.TestOut.Core.Communication;
using ThreeCS.TestOut.Core.Communication.Messages;
using ThreeCS.TestOut.Core.Invokers.Messages;
using ThreeCS.TestOut.Core.Models;
using ThreeCS.TestOut.Core.Servers.Messages;

namespace ThreeCS.TestOut.Core.Servers
{
    /// <summary>
    /// Runs a timer to check the health of all agents and test runs, cancelling them as required.
    /// </summary>
    public class TestHealthMonitor
    {
        private static readonly int HeartbeatTimeoutSeconds = 180; //3 minutes

        readonly ILogger<TestHealthMonitor> _logger;
        readonly ServerTestState _state;
        readonly IMessageBusClient _messageBus;
        readonly TestCompleteHandler _testCompleteHandler;

        private Timer _timer;

        public TestHealthMonitor(
            ILogger<TestHealthMonitor> logger,
            ServerTestState state,
            IMessageBusClient messageBus,
            TestCompleteHandler testCompleteHandler)
        {
            _logger = logger;
            _state = state;
            _messageBus = messageBus;
            _testCompleteHandler = testCompleteHandler;
        }

        public virtual void Init()
        {
            //Connect to the message bus.
            _messageBus.OnMessageReceived<HostDisconnectedMessage>(HandleHostDisconnected);
            _messageBus.OnMessageReceived<AgentTestRunHeartbeatMessage>(HandleAgentHeartbeat);
            _messageBus.OnMessageReceived<AgentTestStatusUpdateMessage>(HandleAgentTestStatusUpdate);
            _messageBus.OnMessageReceived<InvokerHeartbeatMessage>(HandleInvokerHeartbeat);

            //Setup timer for periodic health check.
            _timer = new Timer(30 * 1000);
            _timer.Elapsed += DoHealthCheck;
            _timer.Start();
        }

        private async Task HandleInvokerHeartbeat(string senderId, InvokerHeartbeatMessage message)
        {
            var run = _state.GetServerRun(message.InvocationId);
            if (run != null)
            {
                run.LastInvokerHeartbeatAt = DateTime.Now;
            }
            else
            {
                _logger.LogDebug("Received an invoker heartbeat update from {@senderId} for a run that no longer exists, Ingoring.", senderId);
            }
        }

        private async Task HandleAgentTestStatusUpdate(string senderId, AgentTestStatusUpdateMessage message)
        {
            var run = _state.GetAgentDelegatedRun(message.AgentRunRequestId);
            if (run != null)
            {
                run.LastTestActivity = DateTime.Now;
            }
            else
            {
                _logger.LogDebug("Received a status update from {@senderId} for a run that no longer exists, Ingoring.", senderId);
            }
        }

        private async Task HandleAgentHeartbeat(string senderId, AgentTestRunHeartbeatMessage message)
        {
            var run = _state.GetAgentDelegatedRun(message.RequestId);
            if (run != null)
            {
                run.LastHeartbeat = DateTime.Now;
            }
            else
            {
                _logger.LogDebug("Received a heartbeat from {@senderId} for a run that no longer exists, Ingoring.", senderId);
            }
        }

        private async Task HandleHostDisconnected(string senderId, HostDisconnectedMessage message)
        {
            //TODO: if an agent is disconnected by being shut down, we want to fail all the stuff associated with it, but that
            //should be coming in in a different  message.

            ////Get any running tests with this agent.
            //_logger.LogDebug($"Cancelling any running tests.");
            //var activeRuns = _state.GetActiveAgentDelegatedRunsForAgent(senderId);
            //if (activeRuns.Any())
            //{
            //    foreach (var run in activeRuns)
            //    {
            //        await FailRun(run);
            //    }
            //}
            //else
            //{
            //    _logger.LogDebug($"No active runs to fail for agent {senderId}.");
            //}
        }

        private async Task FailRun(Core.Models.AgentDelegatedTestRun run)
        {
            
            var invocationId = run.PartialTests.InvocationSpec.Id;
            //Remove the failed run from the state.  Note that if the test run that contained this agent run
            //as already been removed, this will return null.
            var failedRun = _state.RemoveAgentDelegatedRun(invocationId, run.RequestId);

            //Tell the agent to stop the tests.  Note that if the agent is disconnected, this will just go into the ether.
            try
            {
                await _messageBus.SendMessage(run.AgentId, new CancelAgentTestsMessage
                {
                    RequestId = run.RequestId
                });
            }
            catch (Exception ex)
            {
                _logger.LogDebug("Failed to tell agent to stop run. This isn't critical, and is expected if an agent is stopped. Exception: " + ex.ToString());
            }

            //Requeue the failed tests, if they are still available.
            if (failedRun != null)
            {
                foreach (var test in failedRun.PartialTests.TestsToRun)
                {
                    _logger.LogDebug($"Test {test.FullTestName} in failed run {run.RequestId} (agent {run.AgentId}) is being retried.");
                    var repoTest = _state.GetTestResult(invocationId, test.FullTestName);
                    repoTest.AgentId = run.AgentId;
                    repoTest.StartDateTime = DateTime.Now;
                    repoTest.EndDateTime = DateTime.Now;
                    repoTest.Outcome = TestExecutionOutcome.Failed;
                    repoTest.ErrorMessage = $"Agent {run.AgentId} was faulted, this test has failed.";
                    repoTest.AttemptCount++;
                    await _testCompleteHandler.CompleteOrRequeue(run.PartialTests.InvocationSpec, repoTest);
                }
            }
        }

        /// <summary>
        /// Stops the server run.
        /// </summary>
        private async Task FailServerRun(ServerTestRun serverRun)
        {
            var agentRuns = serverRun.AgentRunsByRequestId.Values.ToList();

            //Remove from the running state.
            _state.RemoveTestRun(serverRun.InvocationSpec.Id);

            //Cancel all the runs.  Note that tests won't be requeued, because we have failed the owning server run.
            await Task.WhenAll(agentRuns.Select(n => FailRun(n)));
        }

        private async void DoHealthCheck(object sender, ElapsedEventArgs e)
        {
            try
            {
                var runningAgentRuns = _state.GetActiveAgentDelegatedRuns();
                _logger.LogDebug($"Found {runningAgentRuns.Count} agent runs to check during heartbeat.");

                HashSet<string> checkedAgents = new HashSet<string>();
                var runsToFail = new List<AgentDelegatedTestRun>();
                foreach (var run in runningAgentRuns)
                {
                    _logger.LogDebug($"Checking heartbeat for run on agent '{run.AgentId}");
                    //Look for no heartbeat or test activity.
                    var secondsSinceLastHeartbeat = Convert.ToInt32((DateTime.Now - run.LastHeartbeat).TotalSeconds);
                    var secondsSinceLastActivity = Convert.ToInt32((DateTime.Now - run.LastTestActivity).TotalSeconds);
                    if (secondsSinceLastHeartbeat > HeartbeatTimeoutSeconds)
                    {
                        _logger.LogError($"Heartbeat for agent '{run.AgentId}' exceeded idle timeout of {HeartbeatTimeoutSeconds} seconds (was {secondsSinceLastHeartbeat} seconds), stopping tests.");
                        runsToFail.Add(run);

                    }
                    else if (secondsSinceLastActivity > run.PartialTests.InvocationSpec.TestInactivityTimeoutSeconds)
                    {
                        _logger.LogError($"Activity timout for agent '{run.AgentId}' exceeded idle timeout of {run.PartialTests.InvocationSpec.TestInactivityTimeoutSeconds} seconds (was {secondsSinceLastActivity} seconds), stopping tests.");
                        runsToFail.Add(run);
                    }
                    else if (!checkedAgents.Add(run.AgentId))
                    {
                        _logger.LogWarning($"Found another run for the same agent, shutting this one down.");
                        runsToFail.Add(run);
                    }
                    else
                    {
                        _logger.LogDebug($"Heartbeat of '{run.LastHeartbeat}' for agent {run.AgentId} still has '{HeartbeatTimeoutSeconds - secondsSinceLastHeartbeat}' seconds left, and LastTestActivity of {run.LastTestActivity} still has {run.PartialTests.InvocationSpec.TestInactivityTimeoutSeconds - secondsSinceLastActivity} seconds left.  Leaving for now.");
                    }
                }

                //TODO: could do this in parallel.
                foreach (var run in runsToFail)
                {
                    await FailRun(run);
                }

                foreach (var serverRun in _state.GetActiveServerRuns())
                {
                    //Check the invoker is still alive.
                    _logger.LogDebug($"Checking heartbeat for run on invoker '{serverRun.InvocationSpec.InvokerId}");

                    //TODO: only check invocation heartbeat if the invocation is interactive..
                    var secondsSinceLastHeartbeat = Convert.ToInt32((DateTime.Now - serverRun.LastInvokerHeartbeatAt).TotalSeconds);
                    if (secondsSinceLastHeartbeat > HeartbeatTimeoutSeconds)
                    {
                        _logger.LogError("Heartbeat for invoker '{@invokerId}' exceeded idle timeout of {@heartbeatTimeoutSeconds} seconds (was {@secondsSinceLastHeartbeat} seconds), stopping complete invocation.",
                            serverRun.InvocationSpec.InvokerId, HeartbeatTimeoutSeconds, secondsSinceLastHeartbeat);

                        await FailServerRun(serverRun);
                    }
                    else
                    {
                        //TODO: check if server run has not had any recent activity.

                        //TODO: check if all tests are complete, and just finish and try notify invoker if so, then remove.

                        //Send out an availability broadcast to all agents, if there are any queued tests.
                        if (serverRun.TestsToProcess.Any())
                        {
                            await _messageBus.BroadcastMessage(new RequestAgentsMessage
                            {
                                InvocationSpec = serverRun.InvocationSpec,
                            });
                        }
                        else
                        {
                            _logger.LogDebug("Test heartbeat skipped.  0 tests waiting, but " + serverRun.ProcessedTestsByFullName.Count + " completed out of " + serverRun.TestsByFullName.Count + " total tests.");
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                //Something really bad happened.  We'll log the exception.
                _logger.LogError(ex, "An exception occurred while performing the health check.");
            }
        }
    }
}
