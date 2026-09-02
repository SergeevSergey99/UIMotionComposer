using System.Runtime.CompilerServices;

// The smoke checks are internal on purpose: they are an implementation detail of this package, not
// API for the projects that use it. The EditMode test assembly is the one caller allowed in, so the
// assertions can stay in one place and be reported case by case by the test runner.
[assembly: InternalsVisibleTo("UIMotionComposer.Tests.Editor")]
