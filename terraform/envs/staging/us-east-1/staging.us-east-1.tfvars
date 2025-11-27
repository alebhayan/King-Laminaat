environment = "staging"
region      = "us-east-1"

vpc_cidr_block = "10.20.0.0/16"

public_subnets = {
  a = {
    cidr_block = "10.20.0.0/24"
    az         = "us-east-1a"
  }
  b = {
    cidr_block = "10.20.1.0/24"
    az         = "us-east-1b"
  }
}

private_subnets = {
  a = {
    cidr_block = "10.20.10.0/24"
    az         = "us-east-1a"
  }
  b = {
    cidr_block = "10.20.11.0/24"
    az         = "us-east-1b"
  }
}

app_s3_bucket_name = "CHANGE_ME-app-staging-us-east-1"

db_name     = "fshdb"
db_username = "fshadmin"
db_password = "CHANGE_ME_STRONG_PASSWORD"

api_container_image = "CHANGE_ME_API_IMAGE"
api_container_port  = 8080
api_cpu             = "256"
api_memory          = "512"
api_desired_count   = 2

blazor_container_image = "CHANGE_ME_BLAZOR_IMAGE"
blazor_container_port  = 8080
blazor_cpu             = "256"
blazor_memory          = "512"
blazor_desired_count   = 2
