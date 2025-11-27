Terraform infrastructure for deploying the fullstackhero .NET starter kit to AWS using ECS Fargate.

This folder assumes:
- Terraform 1.5+ installed.
- AWS account and credentials configured (AWS CLI or env vars).
- Docker installed for building API and Blazor images.

Structure:
- `bootstrap`: creates the remote state S3 bucket.
- `modules`: reusable building blocks (network, ECS, RDS, ElastiCache, S3, app stack).
- `envs`: environment and region specific stacks (`dev`, `staging`, `prod`).

Environments and regions:
- Each environment (dev, staging, prod) can have one or more regions.
- The pattern is `envs/<env>/<region>` (for example `envs/dev/us-east-1`).

## 1. Bootstrap remote Terraform state

1. Go to the bootstrap folder:
   - `cd terraform/bootstrap`
2. Initialize Terraform:
   - `terraform init`
3. Apply to create the S3 state bucket (pick a globally unique bucket name and region):
   - `terraform apply -var="region=us-east-1" -var="bucket_name=your-unique-tf-state-bucket"`
4. Note the bucket name output or reuse the value you passed.

This step only needs to be done once per AWS account.

## 2. Configure backends per environment/region

For each environment/region folder:
- `terraform/envs/dev/us-east-1/backend.tf`
- `terraform/envs/staging/us-east-1/backend.tf`
- `terraform/envs/prod/us-east-1/backend.tf`

Update:
- `bucket` to your state bucket name from step 1.
- `region` to the bucket’s region.
- `key` can remain as-is or be adjusted to your preferred naming.

Example (`envs/dev/us-east-1/backend.tf`):
- `bucket = "your-unique-tf-state-bucket"`
- `key    = "dev/us-east-1/terraform.tfstate"`
- `region = "us-east-1"`

## 3. Build and push Docker images

The API and Blazor containers are built from the Dockerfiles:
- API: `src/Playground/Playground.Api/Dockerfile`
- Blazor: `src/Playground/Playground.Blazor/Dockerfile`

Typical flow (ECR example, per region):
1. Create ECR repositories (once):
   - `aws ecr create-repository --repository-name fsh-playground-api`
   - `aws ecr create-repository --repository-name fsh-playground-blazor`
2. Authenticate Docker to ECR (example for `us-east-1`):
   - `aws ecr get-login-password --region us-east-1 | docker login --username AWS --password-stdin <account-id>.dkr.ecr.us-east-1.amazonaws.com`
3. Build images:
   - `docker build -f src/Playground/Playground.Api/Dockerfile -t fsh-playground-api:latest .`
   - `docker build -f src/Playground/Playground.Blazor/Dockerfile -t fsh-playground-blazor:latest .`
4. Tag for ECR:
   - `docker tag fsh-playground-api:latest <account-id>.dkr.ecr.us-east-1.amazonaws.com/fsh-playground-api:latest`
   - `docker tag fsh-playground-blazor:latest <account-id>.dkr.ecr.us-east-1.amazonaws.com/fsh-playground-blazor:latest`
5. Push:
   - `docker push <account-id>.dkr.ecr.us-east-1.amazonaws.com/fsh-playground-api:latest`
   - `docker push <account-id>.dkr.ecr.us-east-1.amazonaws.com/fsh-playground-blazor:latest`

Use the pushed image URIs in the Terraform `*.tfvars` files.

## 4. Configure environment variables and settings (tfvars)

Each environment/region has a `*.tfvars` file:
- `envs/dev/us-east-1/dev.us-east-1.tfvars`
- `envs/staging/us-east-1/staging.us-east-1.tfvars`
- `envs/prod/us-east-1/prod.us-east-1.tfvars`

These files control:
- VPC CIDR and subnets (`vpc_cidr_block`, `public_subnets`, `private_subnets`).
- Application S3 bucket (`app_s3_bucket_name`).
- Database settings (`db_name`, `db_username`, `db_password`).
- Container images and sizing (`api_*`, `blazor_*` variables).

Update at least:
- `app_s3_bucket_name` → unique bucket names per env.
- `db_password` → strong passwords per env.
- `api_container_image` and `blazor_container_image` → ECR image URIs from step 3.
- CPU/memory/desired_count per environment to match your requirements.

## 5. Deploy an environment (example: dev/us-east-1)

1. Go to the environment folder:
   - `cd terraform/envs/dev/us-east-1`
2. Initialize Terraform with the configured backend:
   - `terraform init`
3. Review the plan with the dev variables:
   - `terraform plan -var-file="dev.us-east-1.tfvars"`
4. Apply:
   - `terraform apply -var-file="dev.us-east-1.tfvars"`

Terraform will:
- Create VPC, subnets, NAT, and routing.
- Create an ECS cluster and Fargate services (API and Blazor).
- Create an internet-facing ALB and target groups.
- Create RDS PostgreSQL and ElastiCache Redis (private).
- Create the application S3 bucket.

Useful outputs:
- `alb_dns_name` → entrypoint DNS for Blazor UI (root path) and API (`/api`).
- `rds_endpoint` → DB host for app configuration.
- `redis_endpoint` → Redis host for caching configuration.

## 6. Deploy staging and prod

Repeat the same steps for staging and prod:
- `cd terraform/envs/staging/us-east-1`
  - `terraform init`
  - `terraform plan -var-file="staging.us-east-1.tfvars"`
  - `terraform apply -var-file="staging.us-east-1.tfvars"`

- `cd terraform/envs/prod/us-east-1`
  - `terraform init`
  - `terraform plan -var-file="prod.us-east-1.tfvars"`
  - `terraform apply -var-file="prod.us-east-1.tfvars"`

Ensure their `*.tfvars` files reference the correct image URIs, CIDRs, and stronger sizing.

## 7. Multi-region support

To add another region (for example `eu-central-1`) for a given environment:
1. Copy an existing region folder:
   - `envs/dev/us-east-1` → `envs/dev/eu-central-1`
2. Adjust `backend.tf`:
   - `key` (for example `dev/eu-central-1/terraform.tfstate`)
   - `region` to the new region if the state bucket is regional or accessed from that region.
3. Update the `*.tfvars` file:
   - `environment` (if needed) and `region`.
   - VPC and subnet CIDRs that do not overlap with other regions.
   - S3 bucket name (must be globally unique).
   - ECR image URIs for the new region if you mirror images there.
4. Run `terraform init`, `plan`, and `apply` as usual in that folder.

## 8. Destroying an environment

To remove resources for a specific env/region (for example dev/us-east-1):
1. Go to the folder:
   - `cd terraform/envs/dev/us-east-1`
2. Run:
   - `terraform destroy -var-file="dev.us-east-1.tfvars"`

This will delete all resources managed by that state. Use with care, especially in staging/prod.
