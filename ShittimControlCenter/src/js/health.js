// What the status bar says about the server, from the two facts anything actually knows: whether the process the Control Center spawned is still alive, and what the loopback probe got back. A process that is up but has not answered within the window it was given is not "starting" any more, and saying so is the difference between waiting and going to look at the console.
// The probe outranks the process because the server is just as often started outside the Control Center, in which case there is no child to ask about.
// `grace` is null for a source run, where `dotnet run` compiles first and a long silence is not a symptom of anything.
export function serverHealth({ proc, startedAt, live, ready, now, grace }) {
  if (ready) return 'online';
  const overdue = grace != null && startedAt != null && now - startedAt > grace;
  if (live) return overdue ? 'unhealthy' : 'starting';
  if (proc !== 'running') return proc === 'failed' ? 'failed' : 'stopped';
  return overdue ? 'unresponsive' : 'starting';
}
