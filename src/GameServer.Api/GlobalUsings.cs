global using System;
global using System.Buffers;
global using System.Diagnostics;
global using System.Net.WebSockets;
global using System.Text.Json;
global using System.Threading;
global using System.Threading.Tasks;

global using Microsoft.AspNetCore.Builder;
global using Microsoft.AspNetCore.Http;
global using Microsoft.Extensions.DependencyInjection;
global using Microsoft.Extensions.Logging;

global using Serilog;

global using GameServer.Api.Logging;
global using GameServer.Api.Middleware;
global using GameServer.Application;
global using GameServer.Application.Common.Interfaces;
global using GameServer.Domain.Enums;
global using GameServer.Infrastructure;
