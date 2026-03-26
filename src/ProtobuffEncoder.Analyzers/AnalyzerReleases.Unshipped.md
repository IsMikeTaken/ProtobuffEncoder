### New Rules

Rule ID | Category | Severity | Notes
--------|----------|----------|-------
PROTO001 | ProtobuffEncoder | Warning | ProtoContractAnalyzer, ProtoContract has no serialisable fields
PROTO002 | ProtobuffEncoder | Error | ProtoContractAnalyzer, Duplicate protobuf field number
PROTO003 | ProtobuffEncoder | Error | ProtoContractAnalyzer, ProtoContract type has no parameterless constructor
PROTO004 | ProtobuffEncoder | Warning | ProtoContractAnalyzer, Serialised property has no setter
PROTO005 | ProtobuffEncoder | Warning | ProtoFieldAnalyzer, ProtoField used without ProtoContract
PROTO006 | ProtobuffEncoder | Error | ProtoFieldAnalyzer, Invalid protobuf field number
PROTO007 | ProtobuffEncoder | Warning | ProtoFieldAnalyzer, Reserved protobuf field number
PROTO008 | ProtobuffEncoder | Info | ProtoContractAnalyzer, Mutable struct as ProtoContract
PROTO009 | ProtobuffEncoder | Warning | ProtoContractAnalyzer, OneOf group has only one member
PROTO010 | ProtobuffEncoder | Warning | ProtoFieldAnalyzer, Unrecognised encoding name
PROTO011 | ProtobuffEncoder | Warning | ProtoServiceAnalyzer, ProtoService has no methods
PROTO012 | ProtobuffEncoder | Error | ProtoServiceAnalyzer, Streaming method has wrong return type
PROTO013 | ProtobuffEncoder | Error | ProtoIncludeAnalyzer, ProtoInclude field number conflicts with ProtoField
PROTO014 | ProtobuffEncoder | Error | ProtoIncludeAnalyzer, ProtoInclude type is not a subclass
PROTO015 | ProtobuffEncoder | Error | ProtoIncludeAnalyzer, ProtoMap on non-Dictionary property
