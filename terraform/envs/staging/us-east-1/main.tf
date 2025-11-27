terraform {
  required_version = ">= 1.5.0"

  required_providers {
    aws = {
      source  = "hashicorp/aws"
      version = "~> 5.0"
    }
  }
}

provider "aws" {
  region = var.region
}

module "app" {
  source = "../../../modules/app_stack"

  environment = var.environment
  region      = var.region

  vpc_cidr_block  = var.vpc_cidr_block
  public_subnets  = var.public_subnets
  private_subnets = var.private_subnets

  app_s3_bucket_name = var.app_s3_bucket_name

  db_name     = var.db_name
  db_username = var.db_username
  db_password = var.db_password

  api_container_image  = var.api_container_image
  api_container_port   = var.api_container_port
  api_cpu              = var.api_cpu
  api_memory           = var.api_memory
  api_desired_count    = var.api_desired_count
  blazor_container_image = var.blazor_container_image
  blazor_container_port  = var.blazor_container_port
  blazor_cpu             = var.blazor_cpu
  blazor_memory          = var.blazor_memory
  blazor_desired_count   = var.blazor_desired_count
}

output "alb_dns_name" {
  value = module.app.alb_dns_name
}

output "api_url" {
  value = module.app.api_url
}

output "blazor_url" {
  value = module.app.blazor_url
}

output "rds_endpoint" {
  value = module.app.rds_endpoint
}

output "redis_endpoint" {
  value = module.app.redis_endpoint
}
