// $begin{copyright}
//
// This file is part of Bolero
//
// Copyright (c) 2018 IntelliFactory and contributors
//
// Licensed under the Apache License, Version 2.0 (the "License"); you
// may not use this file except in compliance with the License.  You may
// obtain a copy of the License at
//
//     http://www.apache.org/licenses/LICENSE-2.0
//
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or
// implied.  See the License for the specific language governing
// permissions and limitations under the License.
//
// $end{copyright}

namespace Bolero

#nowarn "40" // recursive value `segment` in getSegment

open System
open System.Collections.Generic
open System.Diagnostics.CodeAnalysis
open System.Net
open System.Runtime.CompilerServices
open System.Runtime.InteropServices
open FSharp.Reflection

/// <summary>A router that binds page navigation with Elmish.</summary>
/// <typeparam name="model">The Elmish model type.</typeparam>
/// <typeparam name="msg">The Elmish message type.</typeparam>
/// <category>Routing</category>
type IRouter<'model, 'msg> =
    /// <summary>Get the uri corresponding to <paramref name="model" />.</summary>
    abstract GetRoute : model: 'model -> string

    /// <summary>Get the message to send when the page navigates to <paramref name="uri" />.</summary>
    abstract SetRoute : uri: string -> option<'msg>

/// <summary>A simple hand-written router.</summary>
/// <typeparam name="model">The Elmish model type.</typeparam>
/// <typeparam name="msg">The Elmish message type.</typeparam>
/// <category>Routing</category>
type Router<'model, 'msg> =
    {
        /// <summary>Get the uri corresponding to the model.</summary>
        getRoute: 'model -> string
        /// <summary>Get the message to send when the page navigates to the uri.</summary>
        setRoute: string -> option<'msg>
    }

    interface IRouter<'model, 'msg> with
        member this.GetRoute(model) = this.getRoute model
        member this.SetRoute(uri) = this.setRoute uri

/// <summary>A simple router where the endpoint corresponds to a value easily gettable from the model.</summary>
/// <typeparam name="ep">The routing endpoint type.</typeparam>
/// <typeparam name="model">The Elmish model type.</typeparam>
/// <typeparam name="msg">The Elmish message type.</typeparam>
/// <category>Routing</category>
type Router<'ep, 'model, 'msg> =
    {
        /// <summary>Extract the current endpoint from the model.</summary>
        getEndPoint: 'model -> 'ep
        /// <summary>Get the uri corresponding to an endpoint.</summary>
        getRoute: 'ep -> string
        /// <summary>Get the message to send when the page navigates to an uri.</summary>
        setRoute: string -> option<'msg>
    }

    /// <summary>Get the uri for the given <paramref name="endpoint" />.</summary>
    member this.Link(endpoint) = this.getRoute endpoint

    interface IRouter<'model, 'msg> with
        member this.GetRoute(model) = this.getRoute (this.getEndPoint model)
        member this.SetRoute(uri) = this.setRoute uri

/// <summary>Declare how an F# union case matches to a URI.</summary>
/// <category>Routing</category>
[<AttributeUsage(AttributeTargets.Property, AllowMultiple = false)>]
type EndPointAttribute
    /// <summary>Declare how an F# union case matches to a URI.</summary>
    /// <param name="endpoint">The endpoint URI path.</param>
    (endpoint: string) =
    inherit Attribute()

    let endpoint = endpoint.Trim('/').Split('/')

    /// The path that this endpoint recognizes.
    member this.Path = endpoint

/// <summary>
/// Declare that the given field of an F# union case matches the entire remainder of the URL path.
/// </summary>
/// <category>Routing</category>
[<AttributeUsage(AttributeTargets.Property, AllowMultiple = false)>]
type WildcardAttribute
    /// <summary>
    /// Declare that the given field of an F# union case matches the entire remainder of the URL path.
    /// </summary>
    /// <param name="field">The name of the field. If unspecified, this applies to the last field of the case.</param>
    ([<Optional>] field: string) =
    inherit Attribute()

    /// <summary>The name of the field.</summary>
    member this.Field = field

/// <summary>The kinds of invalid router.</summary>
/// <category>Routing</category>
[<RequireQualifiedAccess>]
type InvalidRouterKind =
    | UnsupportedType of Type
    | ParameterSyntax of UnionCaseInfo * string
    | DuplicateField of UnionCaseInfo * string
    | UnknownField of UnionCaseInfo * string
    | MissingField of UnionCaseInfo * string
    | ParameterTypeMismatch of UnionCaseInfo * string * UnionCaseInfo * string
    | ModifierMismatch of UnionCaseInfo * string * UnionCaseInfo * string
    | IdenticalPath of UnionCaseInfo * UnionCaseInfo
    | RestNotLast of UnionCaseInfo
    | InvalidRestType of UnionCaseInfo
    | MultiplePageModels of UnionCaseInfo

/// <summary>Exception thrown when a router is incorrectly defined.</summary>
/// <category>Routing</category>
exception InvalidRouter of kind: InvalidRouterKind with
    override this.Message =
        let withCase (case: UnionCaseInfo) s =
            $"Invalid router defined for union case {case.DeclaringType.FullName}.{case.Name}: %s{s}"
        match this.kind with
        | InvalidRouterKind.UnsupportedType ty ->
            "Unsupported route type: " + ty.FullName
        | InvalidRouterKind.ParameterSyntax(case, field) ->
            withCase case $"Invalid parameter syntax: {field}"
        | InvalidRouterKind.DuplicateField(case, field) ->
            withCase case $"Field duplicated in the path: {field}"
        | InvalidRouterKind.UnknownField(case, field) ->
            withCase case $"Unknown field in the path: {field}"
        | InvalidRouterKind.MissingField(case, field) ->
            withCase case $"Missing field in the path: {field}"
        | InvalidRouterKind.ParameterTypeMismatch(case, field, otherCase, otherField) ->
            withCase case $"Parameter {field} at the same path position as {otherCase.Name}'s {otherField} but has a different type"
        | InvalidRouterKind.ModifierMismatch(case, field, otherCase, otherField) ->
            withCase case $"Parameter {field} at the same path position as {otherCase.Name}'s {otherField} but has a different modifier"
        | InvalidRouterKind.IdenticalPath(case, otherCase) ->
            withCase case $"Matches the exact same path as {otherCase.Name}"
        | InvalidRouterKind.RestNotLast case ->
            withCase case "{*rest} parameter must be the last fragment"
        | InvalidRouterKind.InvalidRestType case ->
            withCase case "{*rest} parameter must have type string, list or array"
        | InvalidRouterKind.MultiplePageModels case ->
            withCase case "multiple page models on the same case"

/// <summary>A wrapper type to include a model in a router page type.</summary>
/// <seealso href="https://fsbolero.io/docs/Routing#page-models" />
/// <category>Routing</category>
[<CLIMutable>]
type PageModel<'T> = { Model: 'T }

[<AutoOpen>]
module private RouterImpl =
    open System.Text.RegularExpressions

    type ArraySegment<'T> with
        member this.Item with get(i) = this.Array.[this.Offset + i]

    type SegmentParserResult = option<obj * list<string>>
    type SegmentParser = list<string> -> SegmentParserResult
    type SegmentWriter = obj -> list<string>
    type Segment =
        {
            parse: SegmentParser
            write: SegmentWriter
        }

    let fail kind = raise (InvalidRouter kind)

    let inline tryParseBaseType<'T when 'T : (static member TryParse : string * byref<'T> -> bool)> s =
        let mutable out = Unchecked.defaultof<'T>
        if (^T : (static member TryParse : string * byref<'T> -> bool) (s, &out)) then
            Some (box out)
        else
            None

    let inline defaultBaseTypeParser<'T when 'T : (static member TryParse : string * byref<'T> -> bool)> = function
        | [] -> None
        | x :: rest ->
            match tryParseBaseType<'T> x with
            | Some x -> Some (box x, rest)
            | None -> None

    let inline baseTypeSegment<'T when 'T : (static member TryParse : string * byref<'T> -> bool)> () =
        {
            parse = defaultBaseTypeParser<'T>
            write = fun x -> [string x]
        }

    let baseTypes : IDictionary<Type, Segment> = dict [
        typeof<string>, {
            parse = function
                | [] -> None
                | x :: rest -> Some (box (WebUtility.UrlDecode x), rest)
            write = fun x -> [WebUtility.UrlEncode(unbox x)]
        }
        typeof<bool>, {
            parse = defaultBaseTypeParser<bool>
            // `string true` returns capitalized "True", but we want lowercase "true".
            write = fun x -> [(if unbox x then "true" else "false")]
        }
        typeof<Byte>, baseTypeSegment<Byte>()
        typeof<SByte>, baseTypeSegment<SByte>()
        typeof<Int16>, baseTypeSegment<Int16>()
        typeof<UInt16>, baseTypeSegment<UInt16>()
        typeof<Int32>, baseTypeSegment<Int32>()
        typeof<UInt32>, baseTypeSegment<UInt32>()
        typeof<Int64>, baseTypeSegment<Int64>()
        typeof<UInt64>, baseTypeSegment<UInt64>()
        typeof<single>, baseTypeSegment<single>()
        typeof<float>, baseTypeSegment<float>()
        typeof<decimal>, baseTypeSegment<decimal>()
    ]

    let sequenceSegment getSegment (ty: Type) revAndConvert toListAndLength : Segment =
        let itemSegment = getSegment ty
        let rec parse acc remainingLength fragments =
            if remainingLength = 0 then
                Some (revAndConvert acc, fragments)
            else
                match itemSegment.parse fragments with
                | None -> None
                | Some (x, rest) ->
                    parse (x :: acc) (remainingLength - 1) rest
        {
            parse = function
                | x :: rest ->
                    match Int32.TryParse(x) with
                    | true, length -> parse [] length rest
                    | false, _ -> None
                | _ -> None
            write = fun x ->
                let list, (length: int) = toListAndLength x
                string length :: List.collect itemSegment.write list
        }

    let [<Literal>] FLAGS_STATIC =
        Reflection.BindingFlags.Static |||
        Reflection.BindingFlags.Public |||
        Reflection.BindingFlags.NonPublic

    let arrayRevAndUnbox<'T> (l: list<obj>) : 'T[] =
        let a = [|for x in l -> unbox<'T> x|]
        Array.Reverse(a)
        a

    let arrayLengthAndBox<'T> (a: array<'T>) : list<obj> * int =
        [for x in a -> box x], a.Length

    let arraySegment getSegment ty : Segment =
        let arrayRevAndUnbox =
            typeof<Segment>.DeclaringType.GetMethod("arrayRevAndUnbox", FLAGS_STATIC)
                .MakeGenericMethod([|ty|])
        let arrayLengthAndBox =
            typeof<Segment>.DeclaringType.GetMethod("arrayLengthAndBox", FLAGS_STATIC)
                .MakeGenericMethod([|ty|])
        sequenceSegment getSegment ty
            (fun l -> arrayRevAndUnbox.Invoke(null, [|l|]))
            (fun l -> arrayLengthAndBox.Invoke(null, [|l|]) :?> _)

    let listRevAndUnbox<'T> (l: list<obj>) : list<'T> =
        List.map unbox<'T> l |> List.rev

    let listLengthAndBox<'T> (l: list<'T>) : list<obj> * int =
        List.mapFold (fun l e -> box e, l + 1) 0 l

    let listSegment getSegment ty : Segment =
        let listRevAndUnbox =
            typeof<Segment>.DeclaringType.GetMethod("listRevAndUnbox", FLAGS_STATIC)
                .MakeGenericMethod([|ty|])
        let listLengthAndBox =
            typeof<Segment>.DeclaringType.GetMethod("listLengthAndBox", FLAGS_STATIC)
                .MakeGenericMethod([|ty|])
        sequenceSegment getSegment ty
            (fun l -> listRevAndUnbox.Invoke(null, [|l|]))
            (fun l -> listLengthAndBox.Invoke(null, [|l|]) :?> _)

    [<CustomEquality; NoComparison>]
    type ParameterModifier =
        /// No modifier: "/{parameter}"
        | Basic
        /// Rest of the path: "/{*parameter}"
        | Rest of (seq<obj> -> obj) * (obj -> seq<obj>)
        // Optional segment: "/{?parameter}" (TODO)
        //| Optional

        interface IEquatable<ParameterModifier> with
            member this.Equals(that) =
                match this, that with
                | Basic, Basic
                | Rest _, Rest _ -> true
                | _ -> false

    /// A {parameter} path segment.
    type Parameter =
        {
            /// A parameter can be common among multiple union cases.
            /// `index` lists these cases, and for each of them, its total number of fields and the index of the field for this segment.
            index: list<UnionCaseInfo * int * int>
            ``type``: Type
            segment: Segment
            modifier: ParameterModifier
            /// Note that several cases can have the same parameter with different names.
            /// In this case, the name field is taken from the first declared case.
            name: string
        }

    /// Intermediate representation of a path segment.
    type UnionParserSegment =
        | Constant of string
        | Parameter of Parameter

    type UnionCase =
        {
            info: UnionCaseInfo
            ctor: obj[] -> obj
            argCount: int
            segments: UnionParserSegment list
        }

    /// The parser for a union type at a given point in the path.
    type UnionParser =
        {
            /// All recognized "/constant" segments, associated with the parser for the rest of the path.
            constants: IDictionary<string, UnionParser>
            /// The recognized "/{parameter}" segment, if any.
            parameter: option<Parameter * UnionParser>
            /// The union case that parses correctly if the path ends here, if any.
            finalize: option<UnionCase>
        }

    let parseEndPointCasePath (case: UnionCaseInfo) : list<string> =
        case.GetCustomAttributes()
        |> Array.tryPick (function
            | :? EndPointAttribute as e -> Some (List.ofSeq e.Path)
            | _ -> None)
        |> Option.defaultWith (fun () -> [case.Name])

    let isConstantFragment (s: string) =
        not (s.Contains("{"))

    type Unboxer =
        static member List<'T> (items: seq<obj>) : list<'T> =
            [ for x in items -> unbox<'T> x ]

        static member Array<'T> (items: seq<obj>) : 'T[] =
            [| for x in items -> unbox<'T> x |]

    type Decons =
        static member List<'T> (l: list<'T>) : seq<obj> =
            Seq.cast l

        static member Array<'T> (l: 'T[]) : seq<obj> =
            Seq.cast l

    let restModifierFor (ty: Type) case =
        if ty = typeof<string> then
            ty, Rest(
                Seq.cast<string> >> String.concat "/" >> box,
                fun s ->
                    match unbox<string> s with
                    | "" -> Seq.empty
                    | s -> s.Split('/') |> Seq.cast<obj>
            )
        elif ty.IsArray && ty.GetArrayRank() = 1 then
            let elt = ty.GetElementType()
            let unboxer = typeof<Unboxer>.GetMethod("Array", FLAGS_STATIC).MakeGenericMethod([|elt|])
            let decons = typeof<Decons>.GetMethod("Array", FLAGS_STATIC).MakeGenericMethod([|elt|])
            elt, Rest(
                (fun x -> unboxer.Invoke(null, [|x|])),
                (fun x -> decons.Invoke(null, [|x|]) :?> _)
            )
        elif ty.IsGenericType && ty.GetGenericTypeDefinition() = typedefof<list<_>> then
            let targs = ty.GetGenericArguments()
            let unboxer = typeof<Unboxer>.GetMethod("List", FLAGS_STATIC).MakeGenericMethod(targs)
            let decons = typeof<Decons>.GetMethod("List", FLAGS_STATIC).MakeGenericMethod(targs)
            targs.[0], Rest(
                (fun x -> unboxer.Invoke(null, [|x|])),
                (fun x -> decons.Invoke(null, [|x|]) :?> _)
            )
        else
            fail (InvalidRouterKind.InvalidRestType case)

    let fragmentParameterRE = Regex(@"^\{([?*]?)([a-zA-Z0-9_]+)\}$", RegexOptions.Compiled)

    let isPageModel (ty: Type) =
        ty.IsGenericType && ty.GetGenericTypeDefinition() = typedefof<PageModel<_>>

    let findPageModel (case: UnionCaseInfo) =
        ((0, None), case.GetFields())
        ||> Array.fold (fun (i, found) field ->
            i + 1,
            if isPageModel field.PropertyType then
                match found with
                | None -> Some (i, field.PropertyType)
                | Some _ -> fail (InvalidRouterKind.MultiplePageModels case)
            else
                found)
        |> snd

    let getCtor (defaultPageModel: obj -> unit) (case: UnionCaseInfo) =
        let ctor = FSharpValue.PreComputeUnionConstructor(case, true)
        match findPageModel case with
        | None -> ctor
        | Some (i, ty) ->
            let fields = case.GetFields()
            let dummyArgs = Array.zeroCreate fields.Length
            fields |> Array.iteri (fun i field ->
                if field.PropertyType.IsValueType then
                    dummyArgs.[i] <- Activator.CreateInstance(field.PropertyType, true))
            let model = FSharpValue.MakeRecord(ty, [|null|])
            dummyArgs.[i] <- model
            let dummy = ctor dummyArgs
            defaultPageModel dummy
            fun vals ->
                vals.[i] <- model
                ctor vals

    let parseEndPointCase getSegment (defaultPageModel: obj -> unit) (case: UnionCaseInfo) =
        let ctor = getCtor defaultPageModel case
        let fields = case.GetFields()
        let defaultFrags() =
            fields
            |> Array.mapi (fun i p ->
                let ty = p.PropertyType
                if isPageModel ty then None else
                Some <| Parameter {
                    index = [case, fields.Length, i]
                    ``type`` = ty
                    segment = getSegment ty
                    modifier = Basic
                    name = p.Name
                })
            |> Array.choose id
            |> List.ofSeq
        match parseEndPointCasePath case with
        // EndPoint "/"
        | [] -> { info = case; ctor = ctor; argCount = fields.Length; segments = defaultFrags() }
        // EndPoint "/const"
        | [root] when isConstantFragment root ->
            { info = case; ctor = ctor; argCount = fields.Length; segments = Constant root :: defaultFrags() }
        // EndPoint <complex_path>
        | frags ->
            let unboundFields =
                fields
                |> Array.choose (fun f -> if isPageModel f.PropertyType then None else Some f.Name)
                |> HashSet
            let fragCount = frags.Length
            let res =
                frags
                |> List.mapi (fun fragIx frag ->
                    if isConstantFragment frag then
                        Constant frag
                    else
                        let m = fragmentParameterRE.Match(frag)
                        if not m.Success then fail (InvalidRouterKind.ParameterSyntax(case, frag))
                        let fieldName = m.Groups.[2].Value
                        match fields |> Array.tryFindIndex (fun p -> p.Name = fieldName) with
                        | Some i ->
                            let p = fields.[i]
                            if not (unboundFields.Remove(fieldName)) then
                                fail (InvalidRouterKind.DuplicateField(case, fieldName))
                            let ty = p.PropertyType
                            let eltTy, modifier =
                                match m.Groups.[1].Value with
                                | "" -> ty, Basic
                                | "*" ->
                                    if fragIx <> fragCount - 1 then
                                        fail (InvalidRouterKind.RestNotLast case)
                                    restModifierFor ty case
                                | _ -> fail (InvalidRouterKind.ParameterSyntax(case, frag))
                            Parameter {
                                index = [case, fields.Length, i]
                                ``type`` = ty
                                segment = getSegment eltTy
                                modifier = modifier
                                name = p.Name
                            }
                        | None -> fail (InvalidRouterKind.UnknownField(case, fieldName))
                )
            if unboundFields.Count > 0 then
                fail (InvalidRouterKind.MissingField(case, Seq.head unboundFields))
            { info = case; ctor = ctor; argCount = fields.Length; segments = res }

    let rec mergeEndPointCaseFragments (cases: seq<UnionCase>) : UnionParser =
        let constants = Dictionary<string, _>()
        let mutable parameter = None
        let mutable final = None
        cases |> Seq.iter (fun case ->
            match case.segments with
            | Constant s :: rest ->
                let existing =
                    match constants.TryGetValue(s) with
                    | true, x -> x
                    | false, _ -> []
                constants.[s] <- { case with segments = rest } :: existing
            | Parameter param :: rest ->
                match parameter with
                | Some (case', param', ps) ->
                    if param.``type`` <> param'.``type`` then
                        fail (InvalidRouterKind.ParameterTypeMismatch(case', param'.name, case.info, param.name))
                    if param.modifier <> param'.modifier then
                        fail (InvalidRouterKind.ModifierMismatch(case', param'.name, case.info, param.name))
                    let param = { param with index = param.index @ param'.index }
                    parameter <- Some (case', param, { case with segments = rest } :: ps)
                | None ->
                    parameter <- Some (case.info, param, [{ case with segments = rest }])
            | [] ->
                match final with
                | Some case' -> fail (InvalidRouterKind.IdenticalPath(case.info, case'.info))
                | None -> final <- Some case
        )
        {
            constants = dict [
                for KeyValue(s, cases) in constants do
                    yield s, mergeEndPointCaseFragments cases
            ]
            parameter = parameter |> Option.map (fun (_, param, cases) ->
                param, mergeEndPointCaseFragments cases)
            finalize = final
        }

    let parseUnion cases : SegmentParser =
        let parser = mergeEndPointCaseFragments cases
        fun l ->
            let d = Dictionary<UnionCaseInfo, obj[]>()
            let rec run (parser: UnionParser) l =
                let finalize rest =
                    parser.finalize |> Option.map (fun case ->
                        let args =
                            match d.TryGetValue(case.info) with
                            | true, args -> args
                            | false, _ -> Array.zeroCreate case.argCount
                        (case.ctor args, rest))
                let mutable constant = Unchecked.defaultof<_>
                match l with
                | s :: rest when parser.constants.TryGetValue(s, &constant) ->
                    run constant rest
                | l ->
                    parser.parameter
                    |> Option.bind (function
                        | { modifier = Basic } as param, nextParser ->
                            match param.segment.parse l with
                            | None -> None
                            | Some (o, rest) ->
                                for (case, fieldCount, i) in param.index do
                                    let a =
                                        match d.TryGetValue(case) with
                                        | true, a -> a
                                        | false, _ ->
                                            let a = Array.zeroCreate fieldCount
                                            d.[case] <- a
                                            a
                                    a.[i] <- o
                                run nextParser rest
                        | { modifier = Rest(restBuild, _) } as param, nextParser ->
                            let restValues = ResizeArray()
                            let rec parse l =
                                match param.segment.parse l, l with
                                | None, [] ->
                                    for (case, fieldCount, i) in param.index do
                                        let a =
                                            match d.TryGetValue(case) with
                                            | true, a -> a
                                            | false, _ ->
                                                let a = Array.zeroCreate fieldCount
                                                d.[case] <- a
                                                a
                                        a.[i] <- restBuild restValues
                                    run nextParser []
                                | None, _::_ -> None
                                | Some (o, rest), _ ->
                                    restValues.Add(o)
                                    parse rest
                            parse l
                    )
                |> Option.orElseWith (fun () -> finalize l)
            run parser l

    let parseConsecutiveTypes getSegment (tys: Type[]) (ctor: obj[] -> obj) : SegmentParser =
        let fields = Array.map getSegment tys
        fun (fragments: list<string>) ->
            let args = Array.zeroCreate fields.Length
            let rec go i fragments =
                if i = fields.Length then
                    Some (ctor args, fragments)
                else
                    match fields.[i].parse fragments with
                    | None -> None
                    | Some (x, rest) ->
                        args.[i] <- x
                        go (i + 1) rest
            go 0 fragments

    let writeConsecutiveTypes getSegment (tys: Type[]) (dector: obj -> obj[]) : SegmentWriter =
        let fields = tys |> Array.map (fun t -> (getSegment t).write)
        fun (r: obj) ->
            Array.map2 (<|) fields (dector r)
            |> List.concat

    let caseDector (case: UnionCaseInfo) : obj -> obj[] =
        FSharpValue.PreComputeUnionReader(case, true)

    let writeUnionCase (case: UnionCase) =
        let dector = caseDector case.info
        fun o ->
            let vals = dector o
            case.segments |> List.collect (function
                | Constant s -> [s]
                | Parameter({ modifier = Basic } as param) ->
                    let (_, _, i) = param.index |> List.find (fun (case', _, _) -> case' = case.info)
                    param.segment.write vals.[i]
                | Parameter({ modifier = Rest(_, decons) } as param) ->
                    let (_, _, i) = param.index |> List.find (fun (case', _, _) -> case' = case.info)
                    [ for x in decons vals.[i] do yield! param.segment.write x ]
            )

    let unionSegment (getSegment: Type -> Segment) (defaultPageModel: obj -> unit) (ty: Type) : Segment =
        let cases =
            FSharpType.GetUnionCases(ty, true)
            |> Array.map (parseEndPointCase getSegment defaultPageModel)
        let write =
            let writers = Array.map writeUnionCase cases
            let tagReader = FSharpValue.PreComputeUnionTagReader(ty, true)
            fun r -> writers.[tagReader r] r
        let parse = parseUnion cases
        { parse = parse; write = write }

    let tupleSegment getSegment ty =
        let tys = FSharpType.GetTupleElements ty
        let ctor = FSharpValue.PreComputeTupleConstructor ty
        let dector = FSharpValue.PreComputeTupleReader ty
        {
            parse = parseConsecutiveTypes getSegment tys ctor
            write = writeConsecutiveTypes getSegment tys dector
        }

    let recordSegment getSegment ty =
        let tys = FSharpType.GetRecordFields(ty, true) |> Array.map (fun p -> p.PropertyType)
        let ctor = FSharpValue.PreComputeRecordConstructor(ty, true)
        let dector = FSharpValue.PreComputeRecordReader(ty, true)
        {
            parse = parseConsecutiveTypes getSegment tys ctor
            write = writeConsecutiveTypes getSegment tys dector
        }

    let rec getSegment (cache: Dictionary<Type, Segment>) (defaultPageModel: obj -> unit) (ty: Type) : Segment =
        match cache.TryGetValue(ty) with
        | true, x -> unbox x
        | false, _ ->
            // Add lazy version in case ty is recursive.
            let rec segment = ref {
                parse = fun x -> segment.Value.parse x
                write = fun x -> segment.Value.write x
            }
            cache.[ty] <- segment.Value
            let getSegment = getSegment cache ignore
            segment.Value <-
                if ty.IsArray && ty.GetArrayRank() = 1 then
                    arraySegment getSegment (ty.GetElementType())
                elif ty.IsGenericType && ty.GetGenericTypeDefinition() = typedefof<list<_>> then
                    listSegment getSegment (ty.GetGenericArguments().[0])
                elif FSharpType.IsUnion(ty, true) then
                    unionSegment getSegment defaultPageModel ty
                elif FSharpType.IsTuple(ty) then
                    tupleSegment getSegment ty
                elif FSharpType.IsRecord(ty, true) then
                    recordSegment getSegment ty
                else
                    fail (InvalidRouterKind.UnsupportedType ty)
            cache.[ty] <- segment.Value
            segment.Value

/// <summary>Functions for building Routers that bind page navigation with Elmish.</summary>
/// <category>Routing</category>
module Router =

    /// <summary>
    /// Infer a router constructed around an endpoint type <typeparamref name="ep" />.
    /// This type must be an F# union type, and its cases should use <see cref="T:EndPointAttribute" />
    /// to declare how they match to a URI.
    /// </summary>
    /// <param name="makeMessage">Function that creates the message for switching to the page pointed by an endpoint.</param>
    /// <param name="getEndPoint">Function that extracts the current endpoint from the Elmish model.</param>
    /// <param name="defaultPageModel">
    /// Function that indicates the default <see cref="T:PageModel`1" /> for a given endpoint.
    /// Inside this function, call <see cref="M:definePageModel" /> to indicate the page model to use when switching to a new page.
    /// </param>
    /// <returns>A router for the given endpoint type.</returns>
    let inferWithModel<[<DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)>] 'ep, 'model, 'msg>
            (makeMessage: 'ep -> 'msg) (getEndPoint: 'model -> 'ep) (defaultPageModel: 'ep -> unit) =
        let ty = typeof<'ep>
        let cache = Dictionary()
        for KeyValue(k, v) in baseTypes do cache.Add(k, v)
        let frag = getSegment cache (unbox >> defaultPageModel) ty
        {
            getEndPoint = getEndPoint
            getRoute = fun ep ->
                box ep
                |> frag.write
                |> String.concat "/"
            setRoute = fun path ->
                path.Split('/')
                |> List.ofArray
                |> frag.parse
                |> Option.bind (function
                    | x, [] -> Some (unbox<'ep> x |> makeMessage)
                    | _ -> None)
        }

    /// <summary>
    /// Infer a router constructed around an endpoint type <typeparamref name="ep" />.
    /// This type must be an F# union type, and its cases should use <see cref="T:EndPointAttribute" />
    /// to declare how they match to a URI.
    /// </summary>
    /// <param name="makeMessage">Function that creates the message for switching to the page pointed by an endpoint.</param>
    /// <param name="getEndPoint">Function that extracts the current endpoint from the Elmish model.</param>
    /// <returns>A router for the given endpoint type.</returns>
    let infer<[<DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)>] 'ep, 'model, 'msg>
            (makeMessage: 'ep -> 'msg) (getEndPoint: 'model -> 'ep) =
        inferWithModel makeMessage getEndPoint ignore

    /// <summary>
    /// An empty PageModel. Used when constructing an endpoint to pass to methods such as <see cref="M:Router`3.Link" />.
    /// </summary>
    let noModel<'T> = { Model = Unchecked.defaultof<'T> }

    /// <summary>
    /// Define the PageModel for a given endpoint.
    /// Must be called inside the <c>defaultPageModel</c> function passed to <see cref="M:inferWithModel`3" />.
    /// </summary>
    /// <param name="pageModel">
    /// The PageModel, retrieved from the endpoint passed to the function by <see cref="M:inferWithModel`3" />.
    /// </param>
    /// <param name="value">The value of the page model to put inside <paramref name="pageModel" />.</param>
    let definePageModel (pageModel: PageModel<'T>) (value: 'T) =
        pageModel.GetType().GetProperty("Model").SetValue(pageModel, value)

/// <category>Routing</category>
[<Extension>]
type RouterExtensions =

    /// <summary>Create an HTML href attribute pointing to the given endpoint.</summary>
    /// <param name="this">The router.</param>
    /// <param name="endpoint">The router endpoint.</param>
    /// <returns>An <c>href</c> attribute pointing to the given endpoint.</returns>
    [<Extension>]
    static member HRef(this: Router<'ep, _, _>, endpoint: 'ep) : Attr =
        Attr.Make "href" (this.Link endpoint)
