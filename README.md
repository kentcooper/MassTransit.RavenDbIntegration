# MassTransit.RavenDbIntegration
RavenDb Saga persistence for MassTransit 3

## Installation

The library is published on nuget.org.

Use `Install-Package MassTransit.RavenDbIntegration` to install it.

## Usage

When you initialize your document store, call `store.RegisterSagaIdConvention();` after the initialization.

`RavenDbSagaRepository<TSaga>` constructor needs `IDocumentStore` instance as the only parameter.
