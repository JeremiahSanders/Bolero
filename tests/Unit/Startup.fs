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
namespace Bolero.Tests.Web

open System.Security.Claims
open Microsoft.AspNetCore.Authentication.Cookies
open Microsoft.AspNetCore.Authorization
open Microsoft.AspNetCore.Builder
open Microsoft.Extensions.DependencyInjection
open Bolero.Remoting.Server
open Bolero.Server
open Bolero.Tests

module Page =
    open Bolero.Html
    open Bolero.Server.Html

    let index = doctypeHtml {
        head {
            ``base`` { attr.href "/" }
            meta { attr.charset "utf-8" }
        }
        body {
            div { attr.id "app"; rootComp<Bolero.Tests.Client.Tests> }
            script {
                rawHtml """
                    // Used by ElementBinder test:
                    function setContent(element, value) {
                      element.innerHTML = value;
                    }
                """
            }
            script { attr.src "_content/Microsoft.AspNetCore.Components.WebAssembly.Authentication/AuthenticationService.js" }
            boleroScript
        }
    }

type Startup() =

    let mutable items = Map.empty

    let remoteHandler (ctx: IRemoteContext) : Client.Remoting.RemoteApi =
        {
            getValue = fun k -> async {
                return Map.tryFind k items
            }
            setValue = fun (k, v) -> async {
                items <- Map.add k v items
            }
            removeValue = fun k -> async {
                items <- Map.remove k items
            }
            signIn = fun username -> async {
                let claims =
                    match username with
                    | "admin" -> [Claim(ClaimTypes.Role, "admin")]
                    | _ -> []
                try
                    do! ctx.HttpContext.AsyncSignIn(username, claims = claims)
                with exn ->
                    printfn $"{exn}"
            }
            signOut = fun () -> async {
                return! ctx.HttpContext.AsyncSignOut()
            }
            getUsername = ctx.Authorize <| fun () -> async {
                return ctx.HttpContext.User.Identity.Name
            }
            getAdmin = ctx.AuthorizeWith [AuthorizeAttribute(Roles = "admin")] <| fun () -> async {
                return "admin ok"
            }
        }

    member this.ConfigureServices(services: IServiceCollection) =
        services.AddControllersWithViews() |> ignore
        services
            .AddAuthorization()
            .AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
                .AddCookie()
                .Services
            .AddRemoting(remoteHandler)
            .AddBoleroHost(prerendered = false)
            .AddServerSideBlazor()
        |> ignore

    member this.Configure(app: IApplicationBuilder) =
        app .UseAuthentication()
            .UseStaticFiles()
            .UseRouting()
            .UseBlazorFrameworkFiles()
            .UseEndpoints(fun endpoints ->
                endpoints.MapBlazorHub() |> ignore
                endpoints.MapBoleroRemoting() |> ignore
                endpoints.MapFallbackToBolero(Page.index) |> ignore)
        |> ignore
