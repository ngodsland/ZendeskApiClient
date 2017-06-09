﻿using System;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Newtonsoft.Json;
using ZendeskApi.Client.Tests.ResourcesSampleSites;
using ZendeskApi.Client.Models;
using ZendeskApi.Client.Requests;
using ZendeskApi.Client.Responses;
using System.Collections.Generic;
using System.Linq;

namespace ZendeskApi.Client.Tests
{
    public class GroupsResourceSampleSite : SampleSite
    {
        private class State
        {
            public IDictionary<long, Group> Groups = new Dictionary<long, Group>();
        }

        public static Action<IRouteBuilder> MatchesRequest
        {
            get
            {
                return rb => rb
                    .MapGet("api/v2/groups/assignable", (req, resp, routeData) =>
                    {
                        var state = req.HttpContext.RequestServices.GetRequiredService<State>();

                        var groups = state
                            .Groups
                            .Where(x => x.Value.Name.Contains("Assign:true"))
                            .Select(p => p.Value);

                        resp.StatusCode = (int)HttpStatusCode.OK;
                        return resp.WriteAsync(JsonConvert.SerializeObject(new GroupsResponse { Item = groups }));
                    })
                    .MapGet("api/v2/groups/{id}", (req, resp, routeData) =>
                    {
                        var id = long.Parse(routeData.Values["id"].ToString());

                        var state = req.HttpContext.RequestServices.GetRequiredService<State>();

                        if (!state.Groups.ContainsKey(id))
                        {
                            resp.StatusCode = (int)HttpStatusCode.NotFound;
                            return Task.CompletedTask;
                        }

                        var group = state.Groups.Single(x => x.Key == id).Value;

                        resp.StatusCode = (int)HttpStatusCode.OK;
                        return resp.WriteAsync(JsonConvert.SerializeObject(new GroupResponse { Item = group }));
                    })
                    .MapGet("api/v2/groups", (req, resp, routeData) =>
                    {
                        var state = req.HttpContext.RequestServices.GetRequiredService<State>();

                        resp.StatusCode = (int)HttpStatusCode.OK;
                        return resp.WriteAsync(JsonConvert.SerializeObject(new GroupsResponse { Item = state.Groups.Values }));
                    })
                    .MapGet("api/v2/users/{id}/groups", (req, resp, routeData) =>
                    {
                        var id = long.Parse(routeData.Values["id"].ToString());

                        var state = req.HttpContext.RequestServices.GetRequiredService<State>();

                        var groups = state
                            .Groups
                            .Where(x => x.Value.Name.Contains($"USER: {id}"))
                            .Select(p => p.Value);

                        resp.StatusCode = (int)HttpStatusCode.OK;
                        return resp.WriteAsync(JsonConvert.SerializeObject(new GroupsResponse { Item = groups }));
                    })
                    .MapPost("api/v2/groups", (req, resp, routeData) =>
                    {
                        var group = req.Body.Deserialize<GroupRequest>().Item;

                        if (group.Name.Contains("error"))
                        {
                            resp.StatusCode = (int)HttpStatusCode.PaymentRequired; // It doesnt matter as long as not 201

                            return Task.CompletedTask;
                        }

                        var state = req.HttpContext.RequestServices.GetRequiredService<State>();

                        group.Id = long.Parse(RAND.Next().ToString());
                        state.Groups.Add(group.Id.Value, group);

                        resp.StatusCode = (int)HttpStatusCode.Created;
                        return resp.WriteAsync(JsonConvert.SerializeObject(new GroupResponse { Item = group }));
                    })
                    .MapPut("api/v2/groups/{id}", (req, resp, routeData) =>
                    {
                        var id = long.Parse(routeData.Values["id"].ToString());

                        var group = req.Body.Deserialize<GroupRequest>().Item;

                        var state = req.HttpContext.RequestServices.GetRequiredService<State>();

                        state.Groups[id] = group;

                        resp.StatusCode = (int)HttpStatusCode.OK;
                        return resp.WriteAsync(JsonConvert.SerializeObject(new GroupResponse { Item = state.Groups[id] }));
                    })
                    .MapDelete("api/v2/groups/{id}", (req, resp, routeData) =>
                    {
                        var id = long.Parse(routeData.Values["id"].ToString());

                        var state = req.HttpContext.RequestServices.GetRequiredService<State>();

                        state.Groups.Remove(id);

                        resp.StatusCode = (int)HttpStatusCode.NoContent;
                        return Task.CompletedTask;
                    });
            }
        }

        private readonly TestServer _server;

        private HttpClient _client;
        public override HttpClient Client => _client;

        public GroupsResourceSampleSite(string resource)
        {
            var webhostbuilder = new WebHostBuilder();
            webhostbuilder
                .ConfigureServices(services => {
                    services.AddSingleton<State>((_) => new State());
                    services.AddRouting();
                    services.AddMemoryCache();
                })
                .Configure(app =>
                {

                    app.UseRouter(MatchesRequest);
                });

            _server = new TestServer(webhostbuilder);

            RefreshClient(resource);
        }

        public override void RefreshClient(string resource)
        {
            _client = _server.CreateClient();
            _client.BaseAddress = new Uri($"http://localhost/{CreateResource(resource)}");
        }

        private string CreateResource(string resource)
        {
            resource = resource?.Trim('/');

            return resource != null ? resource + "/" : resource;
        }

        public Uri BaseUri
        {
            get { return Client.BaseAddress; }
        }

        public override void Dispose()
        {
            Client.Dispose();
            _server.Dispose();
        }
    }
}
