environment = "dev"
region      = "us-east-1"

vpc_cidr_block = "10.10.0.0/16"

public_subnets = {
  a = {
    cidr_block = "10.10.0.0/24"
    az         = "us-east-1a"
  }
  b = {
    cidr_block = "10.10.1.0/24"
    az         = "us-east-1b"
  }
}

private_subnets = {
  a = {
    cidr_block = "10.10.10.0/24"
    az         = "us-east-1a"
  }
  b = {
    cidr_block = "10.10.11.0/24"
    az         = "us-east-1b"
  }
}

app_s3_bucket_name = "dev-fsh-app-bucket"

db_name     = "fshdb"
db_username = "fshadmin"
db_password = "password123!" # Note: In production, use a more secure method for managing secrets.

api_container_image = "ghcr.io/fullstackhero/fsh-playground-api:1e15726a02d9d12cd95679e5aa3fd7fda1f0620f"
api_container_port  = 8080
api_cpu             = "256"
api_memory          = "512"
api_desired_count   = 1

blazor_container_image = "ghcr.io/fullstackhero/fsh-playground-blazor:1e15726a02d9d12cd95679e5aa3fd7fda1f0620f"
blazor_container_port  = 8080
blazor_cpu             = "256"
blazor_memory          = "512"
blazor_desired_count   = 1

