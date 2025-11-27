terraform {
  required_version = ">= 1.5.0"

  required_providers {
    aws = {
      source  = "hashicorp/aws"
      version = "~> 5.0"
    }
  }
}

resource "aws_security_group" "this" {
  name        = "${var.name}-sg"
  description = "Security group for ElastiCache Redis"
  vpc_id      = var.vpc_id

  ingress {
    from_port       = 6379
    to_port         = 6379
    protocol        = "tcp"
    security_groups = var.allowed_security_group_ids
  }

  egress {
    from_port   = 0
    to_port     = 0
    protocol    = "-1"
    cidr_blocks = ["0.0.0.0/0"]
  }

  tags = var.tags
}

resource "aws_elasticache_subnet_group" "this" {
  name       = "${var.name}-subnets"
  subnet_ids = var.subnet_ids

  tags = var.tags
}

resource "aws_elasticache_replication_group" "this" {
  replication_group_id          = var.name
  description                   = "Redis for ${var.name}"
  engine                        = "redis"
  engine_version                = var.engine_version
  node_type                     = var.node_type
  automatic_failover_enabled    = var.automatic_failover_enabled
  multi_az_enabled              = var.multi_az_enabled
  port                          = 6379
  subnet_group_name             = aws_elasticache_subnet_group.this.name
  security_group_ids            = [aws_security_group.this.id]
  at_rest_encryption_enabled    = true
  transit_encryption_enabled    = true
  auto_minor_version_upgrade    = true
  apply_immediately             = true
  snapshot_retention_limit      = var.snapshot_retention_limit
  snapshot_window               = var.snapshot_window
  maintenance_window            = var.maintenance_window

  tags = var.tags
}

output "primary_endpoint_address" {
  value = aws_elasticache_replication_group.this.primary_endpoint_address
}
