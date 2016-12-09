# SysDepsDOT
Build dynamic system dependencies graphic from very simple text language. Requires GraphViz install to work, add to path.

From > To, auto-build field references, and auto-create Graph.png of your dependencies.

#Comment
SomeSystem
  DataPointX > SomeOtherSystem: DataPointB
    Some sync process, runs every night, etc
  DataPoint Y
SomeOtherSystem
  DataPointA >> SomethingElse: SomeList
  DataPointB
