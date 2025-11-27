environment = "prod"
region      = "us-east-1"

vpc_cidr_block = "10.30.0.0/16"

public_subnets = {
  a = {
    cidr_block = "10.30.0.0/24"
    az         = "us-east-1a"
  }
  b = {
    cidr_block = "10.30.1.0/24"
    az         = "us-east-1b"
  }
}

private_subnets = {
  a = {
    cidr_block = "10.30.10.0/24"
    az         = "us-east-1a"
  }
  b = {
    cidr_block = "10.30.11.0/24"
    az         = "us-east-1b"
  }
}

app_s3_bucket_name = "CHANGE_ME-app-prod-us-east-1"

db_name     = "fshdb"
db_username = "fshadmin"
db_password = "CHANGE_ME_STRONG_PASSWORD"

api_container_image = "CHANGE_ME_API_IMAGE"
api_container_port  = 8080
api_cpu             = "512"
api_memory          = "1024"
api_desired_count   = 3

blazor_container_image = "CHANGE_ME_BLAZOR_IMAGE"
blazor_container_port  = 8080
blazor_cpu             = "512"
blazor_memory          = "1024"
blazor_desired_count   = 3
