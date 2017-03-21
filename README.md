# MassTransit.RavenDbIntegration
RavenDb Saga persistence for MassTransit 3

[![az-github MyGet Build Status](https://www.myget.org/BuildSource/Badge/az-github?identifier=b5d89944-9bb6-4f62-aa11-77807aa395f0)](https://www.myget.org/)
[![NuGet](https://img.shields.io/nuget/v/MassTransit.RavenDbIntegration.svg)](https://www.nuget.org/packages/MassTransit.RavenDbIntegration/)

## Installation

The library is published on nuget.org.

Use `Install-Package MassTransit.RavenDbIntegration` to install it.

## Usage

When you initialize your document store, call `store.RegisterSagaIdConvention();` after the initialization.

`RavenDbSagaRepository<TSaga>` constructor needs `IDocumentStore` instance as the only parameter.
