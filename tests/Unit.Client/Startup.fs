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

// ASP.NET Core and Blazor startup for web tests.
namespace Bolero.Tests.Client

open System.Security.Claims
open System.Threading.Tasks
open Microsoft.AspNetCore.Components.WebAssembly.Hosting
open Microsoft.AspNetCore.Components.Authorization
open Microsoft.Extensions.DependencyInjection
open Bolero.Remoting.Client

type DummyAuthProvider() =
    inherit AuthenticationStateProvider()

    override _.GetAuthenticationStateAsync() =
        let identity = ClaimsIdentity([|Claim(ClaimTypes.Name, "loic")|], "Fake auth type")
        let user = ClaimsPrincipal(identity)
        Task.FromResult(AuthenticationState(user))

module Program =
    [<EntryPoint>]
    let Main args =
        let builder = WebAssemblyHostBuilder.CreateDefault(args)
        builder.RootComponents.Add<Tests>("#app")
        builder.Services.AddBoleroRemoting(builder.HostEnvironment) |> ignore
        builder.Services.AddScoped<AuthenticationStateProvider, DummyAuthProvider>() |> ignore
        builder.Services.AddAuthorizationCore() |> ignore
        builder.Build().RunAsync() |> ignore
        0
