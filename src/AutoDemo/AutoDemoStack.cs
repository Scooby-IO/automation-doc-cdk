using Amazon.CDK;
using Amazon.CDK.AWS.IAM;
using Amazon.CDK.AWS.SSM;
using System.Collections.Generic;
using System;

namespace AutoDemo
{
    public class AutoDemoStack : Stack
    {
        internal AutoDemoStack(Construct scope, string id, IStackProps props = null) : base(scope, id, props)
        {
            
            // account and role info
            var automationAccountId = "123456789";
            var automationWorkerRoleName = $"AWS-SystemsManager-AutomationExecutionRole-{Aws.REGION}";
            var automationAdminRoleName = $"AWS-SystemsManager-AutomationAdministrationRole-{Aws.REGION}";

            //Goes into Automation account
            Role automationAdminRole = new Role(this, "autoAdminRole", new RoleProps
            {
                AssumedBy = new CompositePrincipal(
                    new ServicePrincipal("ssm.amazonaws.com")
                ),
                RoleName = automationAdminRoleName,
                InlinePolicies = new Dictionary<string, PolicyDocument>
                {
                    ["ExecutionPolicy"] = new PolicyDocument(new PolicyDocumentProps
                    {
                        Statements = new PolicyStatement[]
                        {
                            new PolicyStatement( new PolicyStatementProps
                            {
                                Effect = Effect.ALLOW,
                                Resources = new [] { $"arn:aws:iam::*:role/{automationWorkerRoleName}" },//construct this
                                Actions = new []
                                {
                                    "sts:AssumeRole"
                                     
                                }
                            }),
                            new PolicyStatement(new PolicyStatementProps
                            {
                                Effect = Effect.ALLOW,
                                Resources = new [] { "*" },
                                Actions = new [] { "organizations:ListAccountsForParent" }
                            })
                        }
                    })
                }
            });

            var listResourceGroups = new PolicyStatement(new PolicyStatementProps
            {
                Effect = Effect.ALLOW,
                Resources = new[] { "*" },
                Actions = new[]
                                {
                                    "resource-groups:ListGroupResources",
                                    "tag:GetResources"
                                }
            });

            var passRole = new PolicyStatement(new PolicyStatementProps
            {
                Effect = Effect.ALLOW,
                Resources = new[] {
                                Arn.Format(new ArnComponents
                                {
                                    Account = automationAccountId,
                                    Region = "",
                                    Resource = "role",
                                    ResourceName = automationWorkerRoleName,
                                    Service = "iam"
                                }, this)
                            },
                Actions = new[] { "iam:PassRole" }
            });


            var managedPolicyTest = new ManagedPolicy(this, "test-managed-policy", new ManagedPolicyProps
            {
                Statements = new PolicyStatement[]
                {
                    listResourceGroups,
                    passRole
                }
            });


            //Goes into all accounts
            Role automationWorkerRole = new Role(this, "automasterrole", new RoleProps
            {
                AssumedBy = new CompositePrincipal(
                    new AccountPrincipal(automationAccountId),
                    new ServicePrincipal("ssm.amazonaws.com")
                ),
                RoleName = automationWorkerRoleName,
                ManagedPolicies = new IManagedPolicy[] {
                    ManagedPolicy.FromAwsManagedPolicyName("service-role/AmazonSSMAutomationRole"),
                    managedPolicyTest
                },
                Path = "/",
              
                InlinePolicies = new Dictionary<string, PolicyDocument>
                {
                    ["ExecutionPolicy"] = new PolicyDocument(new PolicyDocumentProps
                    {
                        Statements = new PolicyStatement[]
                        {
                            new PolicyStatement( new PolicyStatementProps
                            {
                                Effect = Effect.ALLOW,
                                Resources = new [] { "*" },
                                Actions = new []
                                {
                                    "resource-groups:ListGroupResources",
                                    "tag:GetResources"
                                }
                            }),
                            new PolicyStatement(new PolicyStatementProps
                            {
                                Effect = Effect.ALLOW,
                                Resources = new[] {
                                Arn.Format(new ArnComponents
                                {
                                    Account = automationAccountId,
                                    Region = "",
                                    Resource = "role",
                                    ResourceName = automationWorkerRoleName,
                                    Service = "iam"
                                }, this)
                            },
                            Actions = new [] { "iam:PassRole" }
                            })
                        }
                    })

                }
            });


            User automationUser = new User(this, "automation-user", new UserProps
            {
                UserName = "automationLimited"
            });


            Role myLimitedAssumeRole = new Role(this, "roleToAssume", new RoleProps
            {
                AssumedBy = new ArnPrincipal(Arn.Format(new ArnComponents
                {
                    Account = automationAccountId,
                    Region = "",
                    Resource = "user",
                    ResourceName = automationUser.UserName,
                    Service = "iam"
                }, this)),
                RoleName = $"Automation-Restricted-{Aws.REGION}",
                
                InlinePolicies = new Dictionary<string, PolicyDocument>
                {
                    ["limited-actions"] = new PolicyDocument(new PolicyDocumentProps
                    {
                        Statements = new PolicyStatement[] {
                            new PolicyStatement(new PolicyStatementProps {
                                Resources = new string[]
                                {
                                    "*"
                                },
                                Actions = new string[]
                                {
                                    "ssm:DescribeAutomationExecutions",
                                    "ssm:DescribeAutomationStepExecutions",
                                    "ssm:DescribeDocument",
                                    "ssm:GetAutomationExecution",
                                    "ssm:GetDocument",
                                    "ssm:ListDocuments",
                                    "ssm:ListDocumentVersions",
                                    "ssm:StartAutomationExecution"
                                }
                            }),
                            new PolicyStatement(new PolicyStatementProps {
                                Resources = new string[]
                                {
                                    Arn.Format(new ArnComponents
                                    {
                                    Account = automationAccountId,
                                        Region = "",
                                        Resource = "role",
                                        ResourceName = automationAdminRoleName,
                                        Service = "iam"
                                    }, this)
                                },
                                Actions = new string[]
                                {
                                    "iam:PassRole"
                                },
                                Effect = Effect.ALLOW
                            } )
                        }
                    })
                }
            });


            //Automation document for updating AMI's
            var amiUpdateDoc = new CfnDocument(this, "ami-update-document", new CfnDocumentProps
            {
               // Name = "ami-update",
                DocumentType = "Automation",
                
                Content = new Dictionary<string, object>
                {
                    ["schemaVersion"] = "0.3",
                    ["description"] = "Updates Parameter store with the latest AMI for specific images",
                    ["assumeRole"] = "{{ AutomationAssumeRole }}",
                    ["parameters"] = new Dictionary<string, object>
                    {
                        ["AutomationAssumeRole"] = new Dictionary<string, string>
                        {
                            ["type"] = "String",
                            ["description"] = "(Optional) The ARN of the role that allows Automation to perform the actions on your behalf"
                        },
                        ["AmiID"] = new Dictionary<string, string>
                        {
                            ["type"] = "String",
                            ["description"] = "(Required) The image ID for the new AMI"
                        },
                        ["ImageName"] = new Dictionary<string, string>
                        {
                            ["type"] = "String",
                            ["description"] = "(Required) The name of the image which shoud have the AMI ID updated."
                        }
                    },
                    ["mainSteps"] = new Dictionary<string, object>[] {
                        new Dictionary<string,object> {
                            ["action"] = "aws:executeAwsApi",
                            ["name"] = "getCurrentValue",
                            ["inputs"] = new Dictionary<string,object> {
                                ["Api"] = "GetParameter",
                                ["Name"] = "/amis/{{ ImageName }}/id",
                                ["Service"] = "ssm"
                            },
                            ["outputs"] = new Dictionary<string,object>[] {
                                new Dictionary<string,object> {
                                    ["Name"] = "value",
                                    ["Selector"] = "$.Parameter.Value",
                                    ["Type"] = "String"
                                }
                            }
                        },
                        /*new Dictionary<string,object> {
                            ["action"] = "aws:branch",
                            ["name"] = "confirmChange",
                            ["isEnd"] = true,
                            ["inputs"] = new Dictionary<string,object> {
                                ["Choices"] = new Dictionary<string,object>[] {
                                    new Dictionary<string,object> {
                                        ["NextStep"] = "getDeployRole",
                                        ["Not"] = new Dictionary<string,object> {
                                            ["StringEquals"] = "{{ Version }}",
                                            ["Variable"] = "{{ getCurrentValue.value }}"
                                        }
                                    }
                                }
                            }
                        },*/
                        new Dictionary<string,object> {
                            ["action"] = "aws:executeAwsApi",
                            ["name"] = "putNewVersion",
                            ["inputs"] = new Dictionary<string,object> {
                                ["Api"] = "PutParameter",
                                ["Name"] = "/amis/{{ ImageName }}/id-new",
                                ["Overwrite"] = true,
                                ["Service"] = "ssm",
                                ["Value"] = "{{ AmiID }}",
                                ["Type"] = "String "
                            }
                        }
                    }
                }
            });



            //create the SSM parameters
            var param = "/aws/service/ami-windows-latest/Windows_Server-2019-English-Full-ECS_Optimized/image_id";
            var lookupParam = StringParameter.ValueForTypedStringParameter(this, param);

            var lookupParamTest = StringParameter.ValueForTypedStringParameter(this, param);
            
            // ami-082a23ee4379053a2 - default

            Console.WriteLine(lookupParam);

            new StringParameter(this, $"ami-windows-parameter", new StringParameterProps
            {
                Description = $"The AMI ID for the Windows image",
                ParameterName = $"/amis/windows/id",
                //Type = ParameterType.AWS_EC2_IMAGE_ID,
                StringValue = lookupParam,
                Tier = ParameterTier.STANDARD
            });

            
            new Amazon.CDK.AWS.SSM.CfnParameter(this, "ami-testing", new Amazon.CDK.AWS.SSM.CfnParameterProps
            {
                DataType = "aws:ec2:image",
                Description = "Testing ami data type",
                Name = "/amis/windows-test/id",
                Type = "String",
                Value = lookupParamTest
            });


            new StringParameter(this, "ami-deploy-document-parameter", new StringParameterProps
            {
                Description = "The name of the SSM document for rolling out new AMI's",
                ParameterName = $"/ci-deploy/ci-deploy-document",
                StringValue = amiUpdateDoc.Ref,
                Tier = ParameterTier.STANDARD
            });



        }
    }
}
