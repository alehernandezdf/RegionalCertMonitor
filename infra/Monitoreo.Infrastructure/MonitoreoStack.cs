// BEGIN-FEAT::BE-672::2026-03-17::AHL::CDK Stack con ECS Fargate, RDS PostgreSQL, ECR, CloudWatch, IAM, SGs
using Amazon.CDK;
using Amazon.CDK.AWS.EC2;
using Amazon.CDK.AWS.ECS;
using Amazon.CDK.AWS.ECS.Patterns;
using Amazon.CDK.AWS.ECR;
using Amazon.CDK.AWS.RDS;
using Amazon.CDK.AWS.Logs;
using Amazon.CDK.AWS.IAM;
using Amazon.CDK.AWS.ApplicationAutoScaling;
using Constructs;

namespace Monitoreo.Infrastructure;

public class MonitoreoStack : Stack
{
    public MonitoreoStack(Construct scope, string id, IStackProps? props = null) : base(scope, id, props)
    {
        var environment = new CfnParameter(this, "Environment", new CfnParameterProps
        {
            Type = "String",
            Default = "production",
            AllowedValues = new[] { "production", "staging" }
        });

        // VPC
        var vpc = new Vpc(this, "MonitoreoVpc", new VpcProps
        {
            MaxAzs = 2,
            NatGateways = 1
        });

        // ECR Repository
        var ecrRepo = new Repository(this, "MonitoreoEcr", new RepositoryProps
        {
            RepositoryName = "monitoreo-unificado",
            RemovalPolicy = RemovalPolicy.RETAIN,
            LifecycleRules = new[] { new LifecycleRule { MaxImageCount = 10 } }
        });

        // RDS PostgreSQL
        var dbSecurityGroup = new SecurityGroup(this, "DbSg", new SecurityGroupProps
        {
            Vpc = vpc,
            Description = "Security group for Monitoreo RDS PostgreSQL"
        });

        var database = new DatabaseInstance(this, "MonitoreoDB", new DatabaseInstanceProps
        {
            Engine = DatabaseInstanceEngine.Postgres(new PostgresInstanceEngineProps
            {
                Version = PostgresEngineVersion.VER_16
            }),
            InstanceType = InstanceType.Of(InstanceClass.BURSTABLE3, InstanceSize.MICRO),
            Vpc = vpc,
            VpcSubnets = new SubnetSelection { SubnetType = SubnetType.PRIVATE_WITH_EGRESS },
            SecurityGroups = new[] { dbSecurityGroup },
            DatabaseName = "monitoreo",
            AllocatedStorage = 20,
            MaxAllocatedStorage = 100,
            BackupRetention = Duration.Days(7),
            RemovalPolicy = RemovalPolicy.SNAPSHOT,
            DeletionProtection = true
        });

        // ECS Cluster
        var cluster = new Cluster(this, "MonitoreoCluster", new ClusterProps
        {
            Vpc = vpc,
            ClusterName = "monitoreo-unificado",
            ContainerInsights = true
        });

        // CloudWatch Log Group
        var logGroup = new LogGroup(this, "MonitoreoLogs", new LogGroupProps
        {
            LogGroupName = $"/ecs/monitoreo-unificado/{environment.ValueAsString}",
            Retention = RetentionDays.ONE_MONTH,
            RemovalPolicy = RemovalPolicy.DESTROY
        });

        // Task Role with permissions
        var taskRole = new Role(this, "MonitoreoTaskRole", new RoleProps
        {
            AssumedBy = new ServicePrincipal("ecs-tasks.amazonaws.com")
        });
        taskRole.AddManagedPolicy(ManagedPolicy.FromAwsManagedPolicyName("AmazonSSMReadOnlyAccess"));
        taskRole.AddManagedPolicy(ManagedPolicy.FromAwsManagedPolicyName("SecretsManagerReadWrite"));
        taskRole.AddManagedPolicy(ManagedPolicy.FromAwsManagedPolicyName("AmazonSESFullAccess"));
        taskRole.AddManagedPolicy(ManagedPolicy.FromAwsManagedPolicyName("CloudWatchFullAccess"));

        // Fargate Task Definition
        var taskDef = new FargateTaskDefinition(this, "MonitoreoTask", new FargateTaskDefinitionProps
        {
            MemoryLimitMiB = 512,
            Cpu = 256,
            TaskRole = taskRole
        });

        var container = taskDef.AddContainer("monitoreo-worker", new ContainerDefinitionOptions
        {
            Image = ContainerImage.FromEcrRepository(ecrRepo, "latest"),
            Logging = LogDrivers.AwsLogs(new AwsLogDriverProps { LogGroup = logGroup, StreamPrefix = "worker" }),
            Environment = new Dictionary<string, string>
            {
                ["DOTNET_ENVIRONMENT"] = "Production",
                ["Monitoring__Environment"] = environment.ValueAsString,
                ["ConnectionStrings__PostgreSQL"] = $"Host={database.DbInstanceEndpointAddress};Port={database.DbInstanceEndpointPort};Database=monitoreo;Username=postgres;Password={{resolve:secretsmanager:monitoreo-db-password}}"
            }
        });

        // Fargate Service
        var fargateService = new FargateService(this, "MonitoreoService", new FargateServiceProps
        {
            Cluster = cluster,
            TaskDefinition = taskDef,
            DesiredCount = 1,
            VpcSubnets = new SubnetSelection { SubnetType = SubnetType.PRIVATE_WITH_EGRESS },
            AssignPublicIp = false
        });

        // Allow ECS to connect to RDS
        database.Connections.AllowFrom(fargateService, Port.Tcp(5432), "ECS to RDS");

        // Auto-scaling
        var scaling = fargateService.AutoScaleTaskCount(new EnableScalingProps
        {
            MinCapacity = 1,
            MaxCapacity = 3
        });

        scaling.ScaleOnCpuUtilization("CpuScaling", new CpuUtilizationScalingProps
        {
            TargetUtilizationPercent = 70,
            ScaleInCooldown = Duration.Seconds(60),
            ScaleOutCooldown = Duration.Seconds(60)
        });

        // Outputs
        _ = new CfnOutput(this, "EcrRepoUri", new CfnOutputProps { Value = ecrRepo.RepositoryUri });
        _ = new CfnOutput(this, "ClusterName", new CfnOutputProps { Value = cluster.ClusterName });
        _ = new CfnOutput(this, "DbEndpoint", new CfnOutputProps { Value = database.DbInstanceEndpointAddress });
    }
}
// END-FEAT::BE-672::2026-03-17::AHL::CDK Stack con ECS Fargate, RDS PostgreSQL, ECR, CloudWatch, IAM, SGs
