# Provision a Dynamics Connector Services from GitHub

<a href="https://portal.azure.com/#create/Microsoft.Template/uri/https%3A%2F%2Fraw.githubusercontent.com%2FAzure-Samples%2FDynamicsConnector%2Fmaster%2Fdeployment%2Fazuredeploy.json" target="_blank">
    <img src="http://azuredeploy.net/deploybutton.png"/>
</a>
<a href="http://armviz.io/#/?load=https%3A%2F%2Fraw.githubusercontent.com%2FAzure-Samples%2FDynamicsConnector%2Fmaster%2Fdeployment%2Fazuredeploy.json%3Ftoken%3DAF2BWF4UE3BZMFTZCCMTWLK4ZHM5E" target="_blank">
    <img src="http://armviz.io/visualizebutton.png"/>
</a>

## Overview

![Solution Overview Diagram](/docs/media/DynamicsConnectorLite.jpg)
The Dynamics 365 connector from Microsoft uses a number of Azure services (Azure Functions, Azure Service Bus, and Azure Storage) with custom code and configuration (JSON) files to establish a two-way communication between any SQL DB and Dynamics 365. The connector is built in a modular fashion. One module queries the SQL DB, another connects to Dynamics 365, and code in the middle performs any needed data transformations to convert incoming data into a form that Dynamics 365 can read. With this modular architecture different data sources can be synchronized with Dynamics.
