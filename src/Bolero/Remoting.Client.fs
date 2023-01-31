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

namespace Bolero.Remoting.Client

#nowarn "44" // Ignore obsoleteness of RemoteResponse

open System
open System.Net
open System.Net.Http
open System.Runtime.CompilerServices
open System.Text
open System.Text.Json
open System.Text.Json.Serialization
open Microsoft.AspNetCore.Components.WebAssembly.Hosting
open Microsoft.Extensions.DependencyInjection
open FSharp.Reflection
open Bolero.Remoting

[<Obsolete "Use Cmd.ofAuthorized / performAuthorized">]
type RemoteResponse<'resp> =
    | Success of 'resp
    | Unauthorized

    member this.TryGetResponse() =
        match this with
        | Success x -> Some x
        | Unauthorized -> None

/// <exclude />
[<AllowNullLiteral>]
type IConfigureSerialization =
    abstract ConfigureSerialization: JsonSerializerOptions -> unit

/// <summary>Provides remote service implementations when running in WebAssembly.</summary>
/// <exclude />
type ClientRemoteProvider(http: HttpClient, configureSerialization: IConfigureSerialization) =

    let serOptions = JsonSerializerOptions()
    do configureSerialization.ConfigureSerialization(serOptions)

    let normalizeBasePath (basePath: string) =
        let baseAddress = http.BaseAddress.OriginalString
        let sb = StringBuilder(baseAddress)
        match baseAddress.EndsWith("/"), basePath.StartsWith("/") with
        | true, true -> sb.Append(basePath.[1..]) |> ignore
        | false, false -> sb.Append('/').Append(basePath) |> ignore
        | _ -> sb.Append(basePath) |> ignore
        if not (basePath.EndsWith("/")) then sb.Append('/') |> ignore
        sb.ToString()

    let send (method: HttpMethod) (requestUri: string) (content: obj) =
        let content = JsonSerializer.Serialize(content, serOptions)
        new HttpRequestMessage(method, requestUri,
            Content = new StringContent(content, Encoding.UTF8, "application/json")
        )
        |> http.SendAsync
        |> Async.AwaitTask

    member this.SendAndParse<'T>(method, requestUri, content) = async {
        let! resp = send method requestUri content
        match resp.StatusCode with
        | HttpStatusCode.OK ->
            let! respBody = resp.Content.ReadAsStreamAsync() |> Async.AwaitTask
            return! JsonSerializer.DeserializeAsync<'T>(respBody, serOptions).AsTask() |> Async.AwaitTask
        | HttpStatusCode.Unauthorized ->
            return raise RemoteUnauthorizedException
        | _ ->
            return raise (RemoteException resp)
    }

    member this.MakeRemoteProxy(ty: Type, baseUri: string ref) =
        match RemotingExtensions.ExtractRemoteMethods(ty) with
        | Error errors ->
            raise <| AggregateException(
                "Cannot create remoting handler for type " + ty.FullName,
                [| for e in errors -> exn e |])
        | Ok methods ->
            let ctor = FSharpValue.PreComputeRecordConstructor(ty, true)
            methods
            |> Array.map (fun method ->
                let post =
                    typeof<ClientRemoteProvider>.GetMethod("SendAndParse")
                        .MakeGenericMethod([|method.ReturnType|])
                FSharpValue.MakeFunction(method.FunctionType, fun arg ->
                    let uri = baseUri.Value + method.Name
                    post.Invoke(this, [|HttpMethod.Post; uri; arg|])
                )
            )
            |> ctor

    interface IRemoteProvider with

        member this.GetService<'T>(basePath: string) =
            let basePath = normalizeBasePath(basePath)
            this.MakeRemoteProxy(typeof<'T>, ref basePath) :?> 'T

        member this.GetService<'T when 'T :> IRemoteService>() =
            let basePath = ref ""
            let proxy = this.MakeRemoteProxy(typeof<'T>, basePath) :?> 'T
            basePath.Value <- normalizeBasePath proxy.BasePath
            proxy

/// <summary>Extension methods to enable support for remoting in ProgramComponent.</summary>
[<Extension>]
type ClientRemotingExtensions =

    /// <summary>Enable support for remoting in ProgramComponent when running in WebAssembly.</summary>
    /// <param name="services">The DI service collection.</param>
    /// <param name="env">The WebAssembly host environment.</param>
    /// <param name="configureSerialization">
    /// Callback that configures the JSON serialization for remote arguments and return values.
    /// </param>
    /// <returns>The HttpClient builder for remote calls.</returns>
    [<Extension>]
    static member AddRemoting(services: IServiceCollection, env: IWebAssemblyHostEnvironment, ?configureSerialization: JsonSerializerOptions -> unit) =
        ClientRemotingExtensions.AddRemoting(services,
            (fun httpClient -> httpClient.BaseAddress <- Uri(env.BaseAddress)),
            ?configureSerialization = configureSerialization)

    /// <summary>Enable support for remoting in ProgramComponent when running in WebAssembly.</summary>
    /// <param name="services">The DI service collection.</param>
    /// <param name="configureHttpClient">Callback that configures the HttpClient.</param>
    /// <param name="configureSerialization">
    /// Callback that configures the JSON serialization for remote arguments and return values.
    /// </param>
    /// <returns>The HttpClient builder for remote calls.</returns>
    [<Extension>]
    static member AddRemoting(services: IServiceCollection, configureHttpClient: HttpClient -> unit, ?configureSerialization: JsonSerializerOptions -> unit) : IHttpClientBuilder =
        services.AddSingleton({
            new IConfigureSerialization with
                member _.ConfigureSerialization(serOptions) =
                    match configureSerialization with
                    | None -> serOptions.Converters.Add(JsonFSharpConverter())
                    | Some f -> f serOptions
        }) |> ignore
        services.AddHttpClient<IRemoteProvider, ClientRemoteProvider>(configureHttpClient)
