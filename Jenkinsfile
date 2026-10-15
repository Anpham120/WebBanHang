// WebBanHang CI/CD: GitHub -> Jenkins -> Restore -> Build -> Test -> Publish -> Deploy to IIS -> Verify.
// Runs on the Windows machine (agent label "windows") that has the .NET 8 SDK, Git and IIS.
// The Jenkins controller itself may run on Linux; it only schedules the build on that agent.
// A failure in any stage stops the pipeline, so a broken build never reaches IIS.
pipeline {
    agent { label 'windows' }

    parameters {
        string(name: 'BRANCH', defaultValue: 'main',
               description: 'Branch to build. Only "main" is deployed to IIS; other branches stop after Publish.')
    }

    triggers {
        githubPush() // GitHub webhook -> http://<jenkins>/github-webhook/
    }

    options {
        skipDefaultCheckout(true) // the Checkout stage below clones the requested branch
        disableConcurrentBuilds() // one deployment at a time
        timeout(time: 20, unit: 'MINUTES')
        buildDiscarder(logRotator(numToKeepStr: '20'))
    }

    environment {
        DOTNET_CLI_TELEMETRY_OPTOUT = '1'
        DOTNET_NOLOGO = '1'
        PATH = "C:\\Program Files\\dotnet;${env.PATH}"
        APP_VERSION = "1.0.${env.BUILD_NUMBER}" // shown in the site footer and checked by Verify
        DEPLOY_DIR = 'C:\\wwwroot\\webbanhang'
        SITE_URL = 'http://localhost/webbanhang'
    }

    stages {
        stage('Checkout') {
            steps {
                script {
                    def branch = params.BRANCH ?: 'main'
                    def scmVars = checkout([
                        $class: 'GitSCM',
                        branches: [[name: "*/${branch}"]],
                        userRemoteConfigs: scm.userRemoteConfigs
                    ])
                    env.DEPLOY_ALLOWED = (branch == 'main') ? 'true' : 'false'
                    echo "Branch ${branch} at commit ${scmVars.GIT_COMMIT} - deploy to IIS: ${env.DEPLOY_ALLOWED}"
                }
            }
        }

        stage('Restore') {
            steps {
                bat 'dotnet --version'
                bat 'dotnet restore WebBanHang.sln'
            }
        }

        stage('Build') {
            steps {
                bat 'dotnet build WebBanHang.sln -c Release --no-restore -p:Version=%APP_VERSION%'
            }
        }

        stage('Test') {
            steps {
                bat 'dotnet test WebBanHang.sln -c Release --no-build --logger "junit;LogFilePath=%WORKSPACE%\\test-results\\results.xml"'
            }
            post {
                always {
                    junit 'test-results/*.xml'
                }
            }
        }

        stage('Publish') {
            steps {
                bat 'if exist publish rmdir /s /q publish'
                bat 'dotnet publish src/WebBanHang/WebBanHang.csproj -c Release --no-build -o publish -p:Version=%APP_VERSION%'
            }
        }

        stage('Deploy') {
            when { expression { env.DEPLOY_ALLOWED == 'true' } }
            steps {
                bat 'powershell -NoProfile -ExecutionPolicy Bypass -File scripts/deploy-iis.ps1 -PublishDir publish -DeployDir "%DEPLOY_DIR%"'
            }
        }

        stage('Verify') {
            when { expression { env.DEPLOY_ALLOWED == 'true' } }
            steps {
                bat 'powershell -NoProfile -ExecutionPolicy Bypass -File scripts/verify-deployment.ps1 -Url "%SITE_URL%" -ExpectedVersion "%APP_VERSION%"'
            }
        }
    }

    post {
        success {
            echo "Build ${env.BUILD_NUMBER} succeeded. Deployed to IIS: ${env.DEPLOY_ALLOWED}"
        }
        failure {
            echo 'Pipeline failed. If Deploy was not reached, IIS still runs the previous version.'
        }
    }
}
