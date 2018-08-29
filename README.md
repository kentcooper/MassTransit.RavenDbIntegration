# MassTransit.RavenDbIntegration
RavenDb Saga persistence for MassTransit 5

[![az-github MyGet Build Status](https://www.myget.org/BuildSource/Badge/az-github?identifier=b5d89944-9bb6-4f62-aa11-77807aa395f0)](https://www.myget.org/)
[![NuGet](https://img.shields.io/nuget/v/MassTransit.RavenDbIntegration.svg)](https://www.nuget.org/packages/MassTransit.RavenDbIntegration/)

## Installation

The library is published on nuget.org.

Use `Install-Package MassTransit.RavenDbIntegration` to install it.

## Usage

When you initialize your document store, call `store.RegisterSagaIdConvention();` after the initialization.

`RavenDbSagaRepository<TSaga>` constructor needs `IDocumentStore` instance as the only parameter.

## Important

RavenDb is a document database with fully consistent writes and eventually consistent queries.
It takes some time for RavenDb to update query index and during this time indexes are stale.
MassTransit uses two types of correlation for sagas:

 - Correlation by id using `CorrelateById` method and `Guid` as identity type. In such case
   the repository will fetch saga document by its identity and it will be always consistent.

 - Correlation by other attributes using `CorrelateBy` method and a LINQ expression. For this type
   of correlation, the repository will run a query. RavenDb will create an index automatically
   but the index will be stale for some time. This means that for sagas with high traffic
   this persistence method is not suiable since you will get misfires. This is especially valid
   if you use composite events. You can improve the performance, you can create static indexes
   but still, for sagas where many events can come at the same saga instance at the same time,
   this persistence method is not suitable.
